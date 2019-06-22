using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Verdant
{
    public class VerdantException
    {
        public class GameNotFoundException : Exception { }
        public class ChannelingRequiredException : Exception { }
        public class NoAuthException : Exception { }
    }
}
