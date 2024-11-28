/*
   Copyright 2024 DrMeepso

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cove.Server
{
    public partial class CoveServer
    {
        // purely for debug, yes i know its 100% fucked
        public static void printStringDict(Dictionary<string, object> obj, string sub = "")
        {
            foreach (var kvp in obj)
            {
                if (kvp.Value is Dictionary<string, object>)
                    printStringDict((Dictionary<string, object>)kvp.Value, sub + "." + kvp.Key);
                else if (kvp.Value is Dictionary<int, object>)
                    printArray((Dictionary<int, object>)kvp.Value, sub + "." + kvp.Key);
                else
                    Console.WriteLine($"{sub} {kvp.Key}: {kvp.Value}");
            }
        }
        public static void printArray(Dictionary<int, object> obj, string sub = "")
        {
            foreach (var kvp in obj)
            {
                if (kvp.Value is Dictionary<string, object>)
                    printStringDict((Dictionary<string, object>)kvp.Value, sub + "." + kvp.Key);
                else if (kvp.Value is Dictionary<int, object>)
                    printArray((Dictionary<int, object>)kvp.Value, sub + "." + kvp.Key);
                else
                    Console.WriteLine($"{sub} {kvp.Key}: {kvp.Value}");
            }
        }
    }
}
