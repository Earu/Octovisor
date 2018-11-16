local Task = {}

function Task:Run(f, ...)
	coroutine.wrap(function(...)
        self:resolve(f(...))
    end)(...)
	
	return self
end

function Task:Ressolve(...)
	assert(not self.IsCompleted, "Task ran twice")

    self.IsCompleted = true
	self.Results = { ... }

    for _,f in ipairs(self.Workers) do
        f(...)
    end

	self.Workers = { }
end

function Task:Wait(f)
	if self.IsCompleted then
		f(unpack(self.Results)
	else
		table.insert(self.Workers,f)
	end
end

function Task:Await()
	if self.IsCompleted then
		return unpack(self.Results)
	else
		local co = coroutine.running()
		self:Wait(
			function(...)
				coroutine.resume(co, ...)
			end
		)
	end

	return coroutine.yield()
end

return function ctor(f,...)
    local task = setmetatable({
        IsCompleted = false,
        Results     = {},
        Workers     = {}
    },Task)

    if f then task:Run(f,...) end

    return task
end
