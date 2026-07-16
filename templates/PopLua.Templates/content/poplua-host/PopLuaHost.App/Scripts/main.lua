local util = require("util")

log.info("loaded for " .. host.identity())

local player = host.player("Pombo")
log.warn("score is " .. tostring(player.score))

return util.double(player.score) // 2
