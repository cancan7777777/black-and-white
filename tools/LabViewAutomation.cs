using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;

public static class LabViewAutomation {
    static object Invoke(object target, string method, params object[] args) {
        return target.GetType().InvokeMember(method, BindingFlags.InvokeMethod, null, target, args);
    }
    static void Put(object target, string property, object value) {
        target.GetType().InvokeMember(property, BindingFlags.SetProperty, null, target, new object[] { value });
    }
    static object Get(object target, string property) {
        return target.GetType().InvokeMember(property, BindingFlags.GetProperty, null, target, null);
    }
    public static int Main(string[] args) {
        try {
            object app = Activator.CreateInstance(Type.GetTypeFromProgID("LabVIEW.Application"));
            if (args[0] == "bring-to-front") {
                Invoke(app, "BringToFront");
                Console.WriteLine("LABVIEW_ACTIVATED");
            } else if (args[0] == "state") {
                object vi = Invoke(app, "GetVIReference", args[1], "", false, 0);
                Console.WriteLine("PATH=" + Get(vi, "Path"));
                Console.WriteLine("EXEC_STATE=" + Get(vi, "ExecState"));
                Console.WriteLine("FRONT_PANEL_OPEN=" + Get(vi, "FPWinOpen"));
                Console.WriteLine("FRONT_PANEL_STATE=" + Get(vi, "FPState"));
            } else if (args[0] == "new-template") {
                object vi = Invoke(app, "GetVIReference", args[1], "", false, 0);
                Invoke(vi, "SaveInstrument", args[2], false, false);
                Put(vi, "FPWinOpen", true);
                Console.WriteLine("CREATED " + args[2]);
            } else if (args[0] == "save") {
                string path = Path.GetFullPath(args[1]);
                object vi = Invoke(app, "GetVIReference", path, "", false, 0);
                Invoke(vi, "SaveInstrument", path, false, false);
                Console.WriteLine("SAVED " + path);
            } else if (args[0] == "open") {
                object vi = Invoke(app, "GetVIReference", args[1], "", false, 0);
                Invoke(vi, "OpenFrontPanel", true, 1);
                Console.WriteLine("FRONT_PANEL_STATE=" + Get(vi, "FPState"));
                Console.WriteLine("OPENED " + args[1]);
            } else if (args[0] == "run") {
                object vi = Invoke(app, "GetVIReference", args[1], "", false, 0);
                Invoke(vi, "Run", true);
                Console.WriteLine("RUNNING " + args[1]);
            } else if (args[0] == "call") {
                object vi = Invoke(app, "GetVIReference", args[1], "", true, 0);
                object[] callArgs = { new string[0], new object[0] };
                vi.GetType().InvokeMember("Call", BindingFlags.InvokeMethod, null, vi, callArgs);
                Console.WriteLine("CALLED " + args[1]);
            } else if (args[0] == "describe") {
                object vi = Invoke(app, "GetVIReference", args[1], "", false, 0);
                Invoke(vi, "PrintVIToText", args[2], false, int.Parse(args[3]));
                Console.WriteLine("DESCRIBED " + args[1]);
            } else if (args[0] == "evaluate") {
                JavaScriptSerializer json = new JavaScriptSerializer();
                var spec = json.Deserialize<Dictionary<string,object>>(File.ReadAllText(args[2]));
                object vi = Invoke(app, "GetVIReference", args[1], "", false, 0);
                if(spec.ContainsKey("inputs")) foreach(var input in (Dictionary<string,object>)spec["inputs"])
                    Invoke(vi,"SetControlValue",input.Key,input.Value);
                if(!spec.ContainsKey("run") || (bool)spec["run"]) Invoke(vi,"Run",false);
                var result=new Dictionary<string,object>();
                foreach(object output in (System.Collections.IEnumerable)spec["outputs"])
                    result[(string)output]=Invoke(vi,"GetControlValue",(string)output);
                File.WriteAllText(args[3],json.Serialize(result));
                Console.WriteLine("EVALUATED " + args[1]);
            }
            return 0;
        } catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }
}
