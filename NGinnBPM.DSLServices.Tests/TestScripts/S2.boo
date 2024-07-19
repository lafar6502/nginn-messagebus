import System
import System.IO
import NLog
import System.Diagnostics


Debugger.Break()

x = 20 * 20
s1 = 'Testing me'
s2 = s1 + ' ' + x

Console.WriteLine("Hello from boo: $s2")
