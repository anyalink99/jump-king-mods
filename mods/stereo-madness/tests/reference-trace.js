// Differential probe: execute preserved reference methods, with visual/audio
// effects stubbed. The reference file remains external research, not mod code.
const fs = require('fs');
const vm = require('vm');
const context = vm.createContext({ gravityMul: 1.916398, inputSmoothingMul: .9,
    viewportHalfMinus150: 330, collisionHazard: 'hazard', collisionSolid: 'solid',
    objectTypeFlyMode: 'fly', objectTypeCubeMode: 'cube' });
vm.runInContext(fs.readFileSync(process.argv[2], 'utf8') + '\nthis.PlayerClass = Player;', context);
const input = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const p = Object.create(context.PlayerClass.prototype);
p._scene = { _playerWorldX: input.x };
p.p = { y: input.y, yVelocity: 0, isFlying: input.ship, onGround: !input.ship, canJump: !input.ship };
p._gameLayer = { getNearbySectionObjects: () => input.objects, getFloorY: () => 0, getCeilingY: () => input.ship ? 600 : null };
p.runRotateAction = () => {}; p.stopRotation = () => {}; p.killPlayer = () => { p.p.isDead = true; };
p.hitGround = () => { p.p.yVelocity = 0; p.p.onGround = p.p.canJump = true; p.p.isJumping = false; };
const rows = [];
for (let tick = 0; tick < input.held.length; tick++) {
    if (p.p.isDead) break;
    p.p.upKeyDown = input.held[tick];
    for (let s = 0; s < 4 && !p.p.isDead; s++) {
        p.p.lastY = p.p.y;
        p.updateJump(.225); p.p.y += p.p.yVelocity * .225;
        p.checkCollisions(p._scene._playerWorldX - 330);
        p._scene._playerWorldX += 11.540004 * .225;
    }
    rows.push([p._scene._playerWorldX, p.p.y, p.p.yVelocity, !!p.p.onGround, !!p.p.isDead]);
}
fs.writeFileSync(process.argv[4], JSON.stringify(rows));
