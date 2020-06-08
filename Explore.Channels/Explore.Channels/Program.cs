using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Explore.Channels
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }


        static async Task MainAsync(string[] args)
        {
            // Create 
            CreateBoundedChannel();

            CreateBoundedChannelWithOptions();

            var unboundedCh = CreateUnboundedChannel();

            CreateUnboundedChannelWithOptions();

            // Write
            Write(unboundedCh.Writer);

            await WriteAsync(unboundedCh.Writer).ConfigureAwait(false);

            // Read
            var item = Read(unboundedCh.Reader);

            Console.WriteLine(item);

            item = await ReadAsync(unboundedCh.Reader).ConfigureAwait(false);

            Console.WriteLine(item);

            // Merge/Multiplexer

            await Multiplexer().ConfigureAwait(false);

            // Split/Demultiplexer

            await Demultiplexer().ConfigureAwait(false);

            IChannelGenerator chGenerator = new ChannelGenerator();

            var ch = await chGenerator.CreateChannel("System.String") as Channel<string>;
        }

        #region Create Channels

        /// <summary>
        /// Creates the bounded channel with x capacity.
        /// </summary>
        private static Channel<string> CreateBoundedChannel()
        {
            return Channel.CreateBounded<string>(10);
        }

        /// <summary>
        /// Creates the bounded channel with options.
        /// </summary>
        private static Channel<string> CreateBoundedChannelWithOptions()
        {
            return Channel.CreateBounded<string>(new BoundedChannelOptions(10)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,
                SingleWriter = true,
            });
        }

        /// <summary>
        /// Creates the unbounded channel.
        /// </summary>
        private static Channel<string> CreateUnboundedChannel()
        {
            return Channel.CreateUnbounded<string>();
        }

        /// <summary>
        /// Creates the unbounded channel with options.
        /// </summary>
        private static Channel<string> CreateUnboundedChannelWithOptions()
        {
            return Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                AllowSynchronousContinuations = true,
                SingleReader = false,
                SingleWriter = true,
            });
        }

        #endregion

        #region Channel Writer

        /// <summary>
        /// Synchronously writes to the channel.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static void Write(ChannelWriter<string> writer)
        {
            writer.TryWrite("Hi!");
        }

        /// <summary>
        /// Asynchronously writes to the channel.
        /// </summary>
        /// <param name="writer">The writer.</param>
        private static async Task WriteAsync(ChannelWriter<string> writer)
        {
            while (await writer.WaitToWriteAsync())
            {
                if (writer.TryWrite("Hi!!!!!"))
                    return;
            }
        }
        #endregion

        #region Channel Reader

        /// <summary>
        /// Synchronously reads from the channel.
        /// </summary>
        /// <param name="reader">The channel reader.</param>
        private static string Read(ChannelReader<string> reader)
        {
            reader.TryRead(out string item);

            return item;
        }

        /// <summary>
        /// Asynchronously reads from the channel.
        /// </summary>
        /// <param name="reader">The channel reader.</param>
        private static async Task<string> ReadAsync(ChannelReader<string> reader)
        {
            while (await reader.WaitToReadAsync())
            {
                if (reader.TryRead(out string item))
                {
                    return item;
                }
            }
            return string.Empty;
        }
        #endregion


        #region Merge/Multiplexer

        private static async Task Multiplexer()
        {
            var ch1 = CreateUnboundedChannel();
            ch1.Writer.TryWrite("ch1 message");

            var ch2 = CreateUnboundedChannel();
            ch2.Writer.TryWrite("ch2 message");

            ChannelReader<string> mergeReader = Merge(new ChannelReader<string>[] { ch1.Reader, ch2.Reader });

            while(await mergeReader.WaitToReadAsync())
            {
                if(mergeReader.TryRead(out string item))
                {
                    Console.WriteLine(item);
                }
            }
            
        }

        private static ChannelReader<T> Merge<T>(params ChannelReader<T>[] inputs)
        {
            var output = Channel.CreateUnbounded<T>();

            Task.Run(async () =>
            {
                async Task Redirect(ChannelReader<T> input)
                {
                    await foreach (var item in input.ReadAllAsync())
                    {
                        await output.Writer.WriteAsync(item);
                    }
                }

                await Task.WhenAll(inputs.Select(i => Redirect(i)).ToArray());
                output.Writer.Complete();
            });

            return output;
        }

        #endregion


        #region Split/Demultiplexer

        private static async Task Demultiplexer()
        {
            var ch1 = CreateUnboundedChannel();
            ch1.Writer.TryWrite("ch1 message");

            var tasks = new List<Task>();

            IList<ChannelReader<string>> readers = Split(ch1.Reader, 2);

            for (int i = 0; i < readers.Count; i++)
            {
                var reader = readers[i];
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    await foreach (var item in reader.ReadAllAsync())
                        Console.WriteLine(item);
                }));
            }

            await Task.WhenAll(tasks);
        }

        private static IList<ChannelReader<T>> Split<T>(ChannelReader<T> ch, int n)
        {
            var outputs = new Channel<T>[n];

            for (int i = 0; i < n; i++)
            {
                outputs[i] = Channel.CreateUnbounded<T>();

                Task.Run(async () =>
                {
                    var index = 0;
                    await foreach (var item in ch.ReadAllAsync())
                    {
                        await outputs[index].Writer.WriteAsync(item);
                        index = (index + 1) % n;
                    }

                    foreach (var ch in outputs)
                    {
                        ch.Writer.Complete();
                    }
                });
            }

            return outputs.Select(ch => ch.Reader).ToArray();
        }
        #endregion

    }
}
