using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Explore.Channels
{
    public interface IChannelGenerator
    {
        Task<object> CreateChannel(string type);

        Task<object> CreateChannel(string type, UnboundedChannelOptions options);

        Task<object> CreateChannel(string type, int capacity);

        Task<object> CreateChannel(string type, int capacity, BoundedChannelOptions options);
    }
}
