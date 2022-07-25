
using System;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeechToTextGoogle
{
    class Startup
    {

        static public void Main()
        {
            Speech2Text speech = new Speech2Text(16000, "auth.json");

            speech.Start().GetAwaiter().GetResult();

            Thread.Sleep(Timeout.Infinite);
        }
    }
}