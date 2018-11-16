local copas  = require("copas")
local Socket = require("socket")
local JSON   = require("dkjson")
local Task   = require("task")

local Octovisor   = {}
Octovisor.__index = Octovisor

local MessageStatus = {
    DataRequest           = 0,
    DataResponse          = 1,
    ServerError           = 2,
    TargetError           = 3,
    NetworkError          = 4,
    MalformedMessageError = 5,
    ProcessNotFound       = 6,
}

local MessageFinalizer = "__END__"

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
            coroutine.yield()
        end
    end
end

local BufferSize = 256
-- The handler for the receiving coroutine
local function ReceivingHandler(octoclient)
    while octoclient.IsConnected do
        local data,err,partial = octoclient.Socket:receive(BufferSize)
        if not data and err == "timeout" then
    		data = partial
    		err,partial = nil,nil
    	end
        if data and err and partial then assert(false,"???") end

        if data then
            local msg = JSON.decode(data:sub(1,data:len() - MessageFinalizer:len()))
            octoclient:Printf("Received %d bytes",data:len())

            if msg.status == MessageStatus.DataResponse then
                octoclient:HandleDataCallback(msg)
            elseif msg.status == MessageStatus.DataRequest then
                octoclient:HandleDataRequest(msg)
            else
                octoclient:Printf("??? message status was %d",msg.status)
            end
        else
            local config = octoclient.Config
            octoclient:Printf("Server @ %s:%d is unreachable, closing connection",config.ServerAddress,config.ServerPort)
            octoclient:Close()
        end
    end
end

-- Tries to connect to the Octovisor server specified in the config
function Octovisor:Connect()
    if self.IsConnected then return end
    
    self.Socket = copas.wrap(Socket.tcp6())
    self.Socket:settimeout(0) -- this makes sock:receive() work as copas.receiveParital

    self:Printf("Attempting to connect @ %s:%d",self.Config.ServerAddress,self.Config.ServerPort)

    local success,result = Task(self.Socket.connect,self,self.Config.ServerAddress,self.Config.ServerPort):Await()
    if success then
        self.IsConnected = true
        self.SendingCoroutine = coroutine.create(SendingHandler)
        coroutine.resume(self.SendingCoroutine,self)
    else
        error(result)
    end

    self.ReceivingCoroutine = copas.addthread(function()
        ReceivingHandler(self)
    end)
end

-- Diposes of the OctovisorClient's current instance
function Octovisor:Close()
    if not self.IsConnected then return end
    self:Printf("Closing connection @ %s:%d",self.Config.ServerAddress,self.Config.ServerPort)
    self.IsConnected = false

    return Task(self.Socket.close,self)
end

local CurrentMessageID = 0
-- Sends a raw message to the server
function Octovisor:PushMessage(msg,callback)
    if not type(msg) == "table" then return end

    msg.id = CurrentMessageID
    CurrentMessageID = CurrentMessageID + 1
    table.insert(self.MessageQueue,msg)

    self.DataCallbacks[msg.id] = callback

    if not self.Sending and #self.MessageQueue > 0 then
        coroutine.resume(self.SendingCoroutine)
    end
end

function Octovisor:PopMessage()
    local data = JSON.encode(msg) .. MessageFinalizer
    self:Printf("Sending %d bytes",data:len())
    local bytesent,err = self.Socket:send(data)
    table.remove(self.MessageQueue,1)

    return bytesent,err
end

-- Notifies the server that we want to send and receive data
function Octovisor:Register()
    self:Printf("Registering %s",self.Config.ProcessName)
    local t = Task()
    self:PushMessage({
        origin     = self.Config.ProcessName,
        target     = "SERVER",
        identifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
        data       = self.Config.Token,
        status     = MessageStatus.DataRequest,
    },function(data) 
        t:Ressolve(data) 
    end)

    return t:Await()
end

-- Notifies the server that we dont want to receive or send data anymore
function Octovisor:Unregister()
    self:Printf("Unregistering %s",self.Config.ProcessName)
    local t = Task()
	self:PushMessage({
        origin     = self.Config.ProcessName,
        target     = "SERVER",
        identifier = "INTERNAL_OCTOVISOR_PROCESS_END",
        data       = self.Config.Token,
        status     = MessageStatus.DataRequest,
    },function(data)
        t:Ressolve(data)
    end)

    return t:Await()
end

local RemoteProcess = {}
RemoteProcess.__index = RemoteProcess

-- Ask for a distant process for a specific data, and registers a callback
-- to be called when the data is received
function RemoteProcess:GetData(identifier)
    local t = Task()
    self.Client:PushMessage({
        origin     = self.Client.Config.ProcessName,
        target     = self.Name,
        identifier = identifier,
        status     = MessageStatus.DataRequest,
    },function(data)
        t:Ressolve(data)
    end)

    return t:Await()
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
            msg.target = msg.origin
            msg.origin = target
            msg.status = MessageStatus.DataResponse
            self:PushMessage(msg)
        else
            self:Printf("Callback %s will not be called anymore\n%s",msg.identifier,vargs[1])
            self.DataResponses[msg.identifier] = nil
        end
    end
end

-- Calls the associated callback when responses from the distant processes are received
function Octovisor:HandleDataCallback(msg)
    local callback = self.DataCallbacks[msg.id]
    if callback then
        local requestedata = msg.data and JSON.decode(msg.data) or nil
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
        IsConnected     = false,
        Sending         = false,
        Receiving       = false,
        Config          = config,
        MessageQueue    = {},
        DataResponses   = {},
        DataCallbacks   = {},
    }, Octovisor)
end