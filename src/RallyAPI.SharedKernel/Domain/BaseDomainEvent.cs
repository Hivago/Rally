using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RallyAPI.SharedKernel.Domain
{
   public abstract class BaseDomainEvent : IDomainEvent, INotification
    {
        public Guid EventId { get; }
        public DateTime OccurredAt { get; }

        protected BaseDomainEvent() 
        {
            EventId = Guid.NewGuid();
            OccurredAt = DateTime.UtcNow;
        }
    }
}
