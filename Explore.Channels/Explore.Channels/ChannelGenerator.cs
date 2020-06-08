using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Explore.Channels
{
    public sealed class ChannelGenerator : IChannelGenerator
    {
        public ChannelGenerator()
        {
        }

        public Task<object> CreateChannel(string type)
        {
            var method = this.GetChannelMethod(Type.GetType(type), false, false);

            var channel = method.Invoke(method, null);

            return Task.FromResult(channel);
        }

        public Task<object> CreateChannel(string type, UnboundedChannelOptions options)
        {
            var method = this.GetChannelMethod(Type.GetType(type), false, true);

            var channel = method.Invoke(method, new object[] { options });

            return Task.FromResult(channel);
        }

        public Task<object> CreateChannel(string type, int capacity)
        {
            var method = this.GetChannelMethod(Type.GetType(type), true, false);

            var channel = method.Invoke(method, new object[] { capacity });

            return Task.FromResult(channel);
        }

        public Task<object> CreateChannel(string type, int capacity, BoundedChannelOptions options)
        {
            var method = this.GetChannelMethod(Type.GetType(type), true, true);

            var channel = method.Invoke(method, new object[] { capacity, options });

            return Task.FromResult(channel);
        }

        #region Private Methods

        private MethodInfo GetChannelMethod(Type type, bool bounded, bool options)
        {
            if (bounded)
            {
                return GetBoundedMethod(type, options);
            }
            else
            {
                return GetUnboundedMethod(type, options);
            }
        }

        private MethodInfo GetBoundedMethod(Type type, bool options)
        {
            IEnumerable<MethodInfo> methods = typeof(Channel).GetMethods().Where(m => m.Name == "CreateBounded");

            return options ?
                    methods.Where(m => m.GetParameters().Length > 1).First().MakeGenericMethod(type)
                    : methods.Where(m => m.GetParameters().Length == 1).First().MakeGenericMethod(type);
        }

        private MethodInfo GetUnboundedMethod(Type type, bool options)
        {
            IEnumerable<MethodInfo> methods = typeof(Channel).GetMethods().Where(m => m.Name == "CreateUnbounded");

            return options ?
                    methods.Where(m => m.GetParameters().Length > 0).First().MakeGenericMethod(type)
                    : methods.Where(m => m.GetParameters().Length == 0).First().MakeGenericMethod(type);
        }


        #endregion
    }
}
