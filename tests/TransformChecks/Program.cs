using RlRemapper;
static void Equal(double actual, double expected) { if (Math.Abs(actual - expected) > 0.000001) throw new Exception($"Expected {expected}, got {actual}"); }
var (x,y) = Transform.Apply(.02,.01,.05,0,8); Equal(x,0); Equal(y,0);
(x,y) = Transform.Apply(.99,.05,0,0,8); Equal(y,0); Equal(x,Math.Sqrt(.99*.99+.05*.05));
(x,y) = Transform.Apply(.3,.3,0,.75,8); Equal(x,.3); Equal(y,.3);
(x,y) = Transform.Apply(1,1,0,0,8); (x,y) = Transform.Square(x,y); Equal(x,Math.Sqrt(2)); Equal(y,Math.Sqrt(2));
(x,y) = Transform.Apply(-1,0,0,0,8); Equal(x,-1); Equal(y,0);
Equal(Transform.Stick(-1),-32767); Equal(Transform.Trigger(-1),0); Equal(Transform.Trigger(1),255);
var devices = HidHide.ParseGamingDevices("""[{"friendlyName":"Physical","devices":[{"present":true,"deviceInstancePath":"HID\\VID_1234&PID_5678"},{"present":false,"deviceInstancePath":"HID\\VID_9999"},{"present":true,"deviceInstancePath":"HID\\VID_045E&PID_028E"}]}]""");
if (devices.Count != 1 || devices[0].Name != "Physical") throw new Exception("Device selection did not exclude absent and virtual controllers");
Console.WriteLine("Transform and HidHide device selection checks passed");
