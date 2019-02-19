using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Prism
{
    public class RequestLogOptions
    {
        [Range(0, int.MaxValue, ErrorMessage = "Must be between 0 and int max")]
        public int MaxRequests { get; set; }
    }

    public class RequestLog
    {
        private int Start = 0;
        private int Next = 0;
        private TrackedRequest[] Exchanges;
        private int _maxRequests;

        public RequestLog(IOptions<RequestLogOptions> options)
        {
            _maxRequests = Math.Max(0, options.Value.MaxRequests);
            Exchanges = new TrackedRequest[_maxRequests];
        }

        public void Add(TrackedRequest request)
        {
            lock (Exchanges)
            {
                if ((Next + 1) % _maxRequests == Start)
                {
                    Start = (Start + 1) % _maxRequests;
                }
                Exchanges[Next] = request;
                Next = (Next + 1) % _maxRequests;
            }
        }

        public IEnumerable<TrackedRequest> GetTail(int count)
        {
            return GetTailFromOrder(null, count);
        }

        public IEnumerable<TrackedRequest> GetTailFromOrder(long? order, int count)
        {
            if (Start == Next || count <= 0)
            {
                yield break;
            }
            
            int index = (Next - 1 + _maxRequests) % _maxRequests;

            if (order.HasValue)
            {
                long jump = Exchanges[index].Order - order.Value;
                if (jump >= _maxRequests)
                {
                    yield break;
                }

                index = (index - ((int)jump) + _maxRequests) % _maxRequests;
            }
            else
            {
                order = Exchanges[index].Order;
            }

            while (count > 0)
            {
                var entry = Exchanges[index];
                if (entry.Order <= order)
                {
                    yield return entry;
                }

                if (index == Start)
                {
                    yield break;
                }

                index = (index - 1 + _maxRequests) % _maxRequests;
                count--;
            }
        }
    }
}
