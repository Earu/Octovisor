local copas  = require("copas")
local Socket = require("socket")
local JSON   = require("dkjson")

local Octovisor   = {}
Octovisor.__index = Octovisor

local MessageStatus = {
    DataRequest              = 0,
    DataResponse             = 1,
    ServerError              = 2,
    TargetError              = 3,
    NetworkError             = 4,
    MalformedMessageError    = 5,
    ProcessNotFound          = 6,
    UnknownMessageIdentifier = 7,
}

local MessageFinalizer = "__END__"
local FNil = function() end

-- Your typical printf function
function Octovisor:Printf(str,...)
    if self.Config.Verbose then
        local prefix = ("[Octovisor:%s] >> "):format(self.Config.ProcessName)
        print(prefix .. str:format(...))
    end
end

-- The handler for the sending coroutine
local function SendingHandler(octoclient)
    while octoclient.IsConnected do
        if #octoclient.MessageQueue > 0 then
            octoclient.Sending = true
            local msg = octoclient.MessageQueue[1]
            local bytesent,err = octoclient:PopMessage(msg)
            if not bytesent then
                octoclient:Close()
                error(err)
            end
        else
            octoclient.Sending = false
            copas.sleep(-1)
        end
    end
end

-- The handler for the receiving coroutine
local function ReceivingHandler(octoclient)
    while octoclient.IsConnected do
        local data,err,partial = octoclient.Socket:receive(octoclient.BufferSize)
        if not data and err == "timeout" then
    		data = partial
    		err,partial = nil,nil
    	end
        if data and err and partial then assert(false,"???") end

        if data then
            local msg,err = JSON.decode(data:sub(1,data:len() - MessageFinalizer:len()))
            octoclient:Printf("Received %d bytes (%s)",data:len(),msg.data)

            if msg then
                if msg.status > MessageStatus.DataRequest then
                    octoclient:HandleDataCallback(msg)
                else
                    octoclient:HandleDataRequest(msg)
                end
            else
                octoclient:Printf("Malformed message ??\n%s",err)
            end
        else
            local config = octoclient.Config
            octoclient:Printf("Server @ %s:%d is unreachable, closing connection",config.ServerAddress,config.ServerPort)
            octoclient:Close()
        end
    end
end

-- Tries to connect to the Octovisor server specified in the config
function Octovisor:Connect(callback)
    if self.IsConnected then return end
    callback = callback or FNil

    self.ReceivingCoroutine = copas.addthread(function()
        self.Socket = copas.wrap(self.Config.IPV6 and Socket.tcp6() or Socket.tcp4())
        self.Socket:settimeout(0)

        self:Printf("Attempting to connect @ %s:%d",self.Config.ServerAddress,self.Config.ServerPort)

        local success,result = self.Socket:connect(self.Config.ServerAddress,self.Config.ServerPort)
        if success then
            self.IsConnected = true
            self.SendingCoroutine = copas.addthread(function()
                SendingHandler(self)
            end)
            self:Register(callback)
            ReceivingHandler(self)
        else
            error(result)
        end
    end)
end

-- Diposes of the current instance
function Octovisor:Close()
    if not self.IsConnected then return end
    self:Printf("Closing connection @ %s:%d",self.Config.ServerAddress,self.Config.ServerPort)
    self.IsConnected = false

    return self.Socket:close()
end

-- Sends a raw message to the server
function Octovisor:PushMessage(msg,callback)
    if not type(msg) == "table" then return end
    callback = callback or FNil

    msg.id = self.CurrentMessageID
    self.DataCallbacks[self.CurrentMessageID] = callback

    self.CurrentMessageID = self.CurrentMessageID + 1
    table.insert(self.MessageQueue,msg)

    if not self.Sending and #self.MessageQueue > 0 then
        copas.wakeup(self.SendingCoroutine)
    end
end

function Octovisor:PopMessage(msg)
    local data = JSON.encode(msg) .. MessageFinalizer
    self:Printf("Sending %d bytes (%s)",data:len(),msg.data)
    local bytesent,err = self.Socket:send(data)
    table.remove(self.MessageQueue,1)

    return bytesent,err
end

-- Notifies the server that we want to send and receive data
function Octovisor:Register(callback)
    self:Printf("Registering %s",self.Config.ProcessName)
    self:PushMessage({
        origin     = self.Config.ProcessName,
        target     = "SERVER",
        identifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
        data       = self.Config.Token,
        status     = MessageStatus.DataRequest,
    },function(registered)
        if not registered then
            error("Could not register, check server logs")
        else
            callback()
        end
    end)
end

-- Notifies the server that we dont want to receive or send data anymore
function Octovisor:Unregister()
    self:Printf("Unregistering %s",self.Config.ProcessName)
	self:PushMessage({
        origin     = self.Config.ProcessName,
        target     = "SERVER",
        identifier = "INTERNAL_OCTOVISOR_PROCESS_END",
        data       = self.Config.Token,
        status     = MessageStatus.DataRequest,
    })
end

local RemoteProcess = {}
RemoteProcess.__index = RemoteProcess

-- Ask for a distant process for a specific data, and registers a callback
-- to be called when the data is received
function RemoteProcess:GetData(identifier,callback)
    self.Client:PushMessage({
        origin     = self.Client.Config.ProcessName,
        target     = self.Name,
        identifier = identifier,
        status     = MessageStatus.DataRequest,
    },callback)
end

-- Creates remote process object corresponding to a distant process
function Octovisor:RemoteProcess(procname)
    assert(procname,"You must specify the remote process name")
    return setmetatable({
        Client = self,
        Name   = procname,
    },RemoteProcess)
end

-- This is what we should do we when we receive a message from outside
function Octovisor:DataResponse(identifier,callback)
    self.DataResponses[identifier] = callback
end

-- Calls the right things when a distant process requests a data
function Octovisor:HandleDataRequest(msg)
    local callback = self.DataResponses[msg.identifier]
    if callback then
        local vargs = { pcall(callback) }
        local success = vargs[1]
        table.remove(vargs,1)

        if success then
            msg.data = #vargs > 1 and vargs or vargs[1]
            local target = msg.target
            msg.target   = msg.origin
            msg.origin   = target
            msg.status   = MessageStatus.DataResponse
            self:PushMessage(msg)
        else
            self:Printf("Callback %s will not be called anymore\n%s",msg.identifier,vargs[1])
            self.DataResponses[msg.identifier] = nil
        end
    else
        local target = msg.target
        msg.target   = msg.origin
        msg.origin   = target
        msg.status   = MessageStatus.UnknownMessageIdentifier
        self:PushMessage(msg)
    end
end

local Errors = {
    [MessageStatus.TargetError] = function(procname,identifier)
        return ("Remote process (%s) could not process message (%s)"):format(procname,identifier)
    end,
    [MessageStatus.ServerError] = function(_,identifier)
        return ("Server could not process message (%s)"):format(identifier)
    end,
    [MessageStatus.NetworkError] = function()
        return "Network issues"
    end,
    [MessageStatus.MalformedMessageError] = function()
        return "Malformed message"
    end,
    [MessageStatus.ProcessNotFound] = function(procname)
        return ("Unknown remote process (%s)"):format(procname)
    end,
    [MessageStatus.UnknownMessageIdentifier] = function(procname,identifier)
        return ("Remote process (%s) did not know forwarded message idenfier (%s)"):format(procname,identifier)
    end,
}
-- Calls the associated callback when responses from the distant processes are received
function Octovisor:HandleDataCallback(msg)
    if Errors[msg.status] then
        local err = Errors[msg.status](msg.origin,msg.identifier)
        self:Printf(err)
    end

    local callback = self.DataCallbacks[msg.id]
    if callback then
        local isnil = msg.data == nil
        local requestedata = not isnil and JSON.decode(msg.data) or (not isnil and tostring(msg.data) or nil)
        local success,result = pcall(callback,requestedata)
        if not success then
            self:Printf("Error when requesting data with identifier %s\n%s",msg.identifier,result)
        end
        self.DataCallbacks[msg.id] = nil
    end
end

-- Creates a new instance of an OctovisorClient
function OctovisorClient(config)
    return setmetatable({
        IsConnected      = false,
        Sending          = false,
        Receiving        = false,
        Config           = config,
        BufferSize       = 256,
        CurrentMessageID = 0,
        MessageQueue     = {},
        DataResponses    = {},
        DataCallbacks    = {},
    }, Octovisor)
end