// Standalone numerical checks for the visualizer's circular-size behavior.
// Run with: node tests/CircleSizeChecks.cjs
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const html = fs.readFileSync(path.join(__dirname, '../tools/visualizer/comparison.fragment.html'), 'utf8');
const script = [...html.matchAll(/<script(?:\s[^>]*)?>([\s\S]*?)<\/script>/g)][0]?.[1];
assert.ok(script, 'Visualizer mapping script is missing');
const context = vm.createContext({});
vm.runInContext(script, context);
const map = (x, y, opts) => context.JoystickCompare.remapper(x, y, opts).out;
const near = (a, b) => assert.ok(Math.abs(a - b) < 1e-11, `${a} is not ${b}`);
const rad = {mappingMode:'Radial', innerDeadzone:0, radialAntiDeadzone:0, radialMaxZone:1, radialMaxOutput:1, squareStick:false, squareRoundness:0, radialCircleScale:1};
const ang = {mappingMode:'Angular', innerDeadzone:0, outerRingThreshold:0, snapAngleDeg:0, angularMaxZone:1, angularSquareOutput:false, angularCircleScale:1};
for (const settings of [rad, ang]) {
  const name=settings.mappingMode, prop=name==='Radial'?'radialCircleScale':'angularCircleScale';
  near(map(1,0,settings)[0],1);
  near(map(1/Math.SQRT2,1/Math.SQRT2,settings)[0],1/Math.SQRT2);
  settings[prop]=.7;
  near(map(1,0,settings)[0],.7);
  near(map(1/Math.SQRT2,1/Math.SQRT2,settings)[0],.7/Math.SQRT2);
  settings[prop]=1.2;
  near(map(1,0,settings)[0],1); // square boundary caps radius without distorting angle
  const [x,y]=map(Math.cos(Math.PI/6),Math.sin(Math.PI/6),settings);
  near(x,1);near(y,Math.tan(Math.PI/6)); // flat cardinal edge
  const [dx,dy]=map(1/Math.SQRT2,1/Math.SQRT2,settings);
  near(dx,1.2/Math.SQRT2);near(dy,dx); // curved diagonal edge
  settings[prop]=Math.SQRT2;
  const corner=map(1/Math.SQRT2,1/Math.SQRT2,settings);
  near(corner[0],1);near(corner[1],1);
}
rad.squareStick=true;rad.radialCircleScale=.3;
near(map(1/Math.SQRT2,1/Math.SQRT2,rad)[0],1);
ang.angularSquareOutput=true;ang.angularCircleScale=.3;
near(map(1/Math.SQRT2,1/Math.SQRT2,ang)[0],1);
console.log('Circle size numerical checks passed (Radial, Angular, square bypass, angle preservation).');
