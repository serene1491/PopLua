local util = require("util")

log.info(util.message(host.identity()))

local player = host.player("Serene")
player:add_score(util.score_bonus())

log.warn("score is " .. player.score)

return player.score
