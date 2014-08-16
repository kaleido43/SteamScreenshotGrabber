using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SteamScreenshotGrabber
{
    public class StringPair
    {
        public string String1 = "";
        public string String2 = "";

        public StringPair()
        {
        }

        public StringPair(string s1, string s2) 
        {
            this.String1 = s1;
            this.String2 = s2;
        }
    }
}
