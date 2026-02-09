using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RallyAPI.SharedKernel.Results
{
    public sealed class Error
    {
        public string Code { get; }
        public string Message { get; }

        private Error(string code, string message)
        {
            Code = code;
            Message = message;
        }

        public static Error None => new(string.Empty, string.Empty);
        public static Error NullValue => new("Error.NullValue", "A null value was provided.");

        public static Error Create(string code, string message) => new(code, message);

        // Common errors
        public static Error NotFound(string entity, Guid id) =>
            new($"{entity}.NotFound", $"{entity} with ID {id} was not found.");
        public static Error NotFound(string entity) =>
            new($"{entity}.NotFound", $"{entity} was not found.");

        public static Error Validation(string message) =>
            new("Validation.Error", message);

        public static Error Validation(string message,string detail) =>
            new("Validation.Error", message);

        public static Error Conflict(string message) =>
            new("Conflict.Error", message);

        public static Error Unauthorized(string message = "Unauthorized access.") =>
            new("Unauthorized.Error", message);

        public static Error Forbidden(string message = "Access forbidden.") =>
            new("Forbidden.Error", message);

        public override string ToString() => $"{Code}: {Message}";
    }
}
