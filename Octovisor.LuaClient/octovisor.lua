local copas  = require("copas")
local Socket = require("socket")
local JSON   = require("dkjson")

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
            local data = JSON.encode(msg) .. MessageFinalizer
            octoclient:Printf("Sending %d bytes:\n%s",data:len(),data)
            local bytesent,err = octoclient.Socket:send(data)
            table.remove(octoclient.MessageQueue,1)

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
            octoclient:Printf("Received %d bytes:\n%s",data:len(),data)

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
    self.ReceivingThread = copas.addthread(function()
        local sock = copas.wrap(Socket.tcp6())
		sock:settimeout(0) -- this makes sock:receive() work as copas.receiveParital

        self:Printf("Attempting to connect @ %s:%d",self.Config.ServerAddress,self.Config.ServerPort)

        local success,result = sock:connect(self.Config.ServerAddress,self.Config.ServerPort)
        if success then
            self.IsConnected = true
            self.Socket = sock

            self.SendingThread = copas.addthread(function() SendingHandler(self) end)
        else
            error(result)
        end

        ReceivingHandler(self)
    end)
end

-- Diposes of the OctovisorClient's current instance
function Octovisor:Close()
    if not self.IsConnected then return end
    self:Printf("Closing connection @ %s:%d",self.Config.ServerAddress,self.Config.ServerPort)
    self.IsConnected = false
    self.Socket:close()
end

local CurrentMessageID = 0
-- Sends a raw message to the server
function Octovisor:SendMessage(msg,callback)
    if not type(msg) == "table" then return end

    msg.id = CurrentMessageID
    CurrentMessageID = CurrentMessageID + 1
    table.insert(self.MessageQueue,msg)

    self.DataCallbacks[msg.id] = callback

    if not self.Sending and #self.MessageQueue > 0 then
        copas.wakeup(self.SendingThread)
    end
end

-- Notifies the server that we want to send and receive data
function Octovisor:Register(callback)
	self:Printf("Registering %s",self.Config.ProcessName)
    self:SendMessage({
        origin     = self.Config.ProcessName,
        target     = "SERVER",
        identifier = "INTERNAL_OCTOVISOR_PROCESS_INIT",
        data       = self.Config.Token,
        status     = MessageStatus.DataRequest,
    },callback)
end

-- Notifies the server that we dont want to receive or send data anymore
function Octovisor:Unregister(callback)
	self:Printf("Unregistering %s",self.Config.ProcessName)
	self:SendMessage({
        origin     = self.Config.ProcessName,
        target     = "SERVER",
        identifier = "INTERNAL_OCTOVISOR_PROCESS_END",
        data       = self.Config.Token,
        status     = MessageStatus.DataRequest,
    },callback)
end

local RemoteProcess = {}
RemoteProcess.__index = RemoteProcess

-- Ask for a distant process for a specific data, and registers a callback
-- to be called when the data is received
function RemoteProcess:GetData(identifer,callback)
    self.Client:SendMessage({
        origin    = self.Client.Config.ProcessName,
        target    = self.Name,
        identifer = identifer,
        status    = MessageStatus.DataRequest,
    },callback)
end

-- Creates remote process object corresponding to a distant process
function Octovisor:RemoteProcess(procname)
    return setmetatable({
        Client = self,
        Name   = procname or "UNKNOWN_REMOTE_PROCESS",
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
            self:SendMessage(msg)
        else
            self:Printf("Callback %s will not be called anymore\n%s",msg.identifer,vargs[1])
            self.DataResponses[msg.identifer] = nil
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