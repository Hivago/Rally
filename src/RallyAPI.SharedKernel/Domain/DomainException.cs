using System;
using System.Collections.Generic;

using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace RallyAPI.SharedKernel.Domain
{
    public class DomainException : Exception
    {
        public string Code { get; }

        public DomainException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public static DomainException For(string code, string message)
        {
            return new DomainException(code, message);
        }
    }

}
