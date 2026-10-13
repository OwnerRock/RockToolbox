$CursorSchemeOrder = 'pointer.cur','help.cur','working.ani','busy.ani','precision.cur','beam.cur','handwriting.cur','unavailable.cur','vert.cur','horz.cur','dgn1.cur','dgn2.cur','move.cur','alternate.cur','link.cur','person.cur','pin.cur'

Add-Type -MemberDefinition '[DllImport("user32.dll",SetLastError=true)] public static extern bool SystemParametersInfo(uint a,uint b,IntPtr c,uint d);' -Name Cur -Namespace RockNative -ErrorAction SilentlyContinue
