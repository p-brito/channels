## System.Threading.Channels

### What is a channel? 

A channel is a synchronisation concept which supports passing data between producers and consumers, typically concurrently. One or many producers can write data into the channel, which are then read by one or many consumers.

### Core Concept

The core concept of what is a channel can be visualized in the following example:

```csharp
class MyChannel<T>
{
    private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
    private readonly SemaphoreSlim _sem = new SemaphoreSlim(0);

    public void Write(T item)
    {
        _queue.Enqueue(item);
        _sem.Release();
    }

    public async Task<T> ReadAsync()
    {
        await _sem.WaitAsync();
        _queue.TryDequeue(out T item);
        return item;
    }

} 
```
- `ConcurrentQueue<T>` - used to store the items that will be read by the ReadAsync();
- `SemaphoreSlim` - think that this semaphore like a box of keys, this box is initialized with 0 keys. Every time the Write() adds an item to the queue, the _sem.Release() will add a key to the box. While on the opposite side the ReadAsync() is waiting for a key and when it has a key he knows that there is an item in the queue.

This example is only for you to understand the concept of the System.Threading.Channels, you don't need to write this code to use channels.

### Implementation

The `Channel<T>` is the implementation of the abstract class `Channel<T, T>` which is available for the niche uses cases where a channel may itself transform written data into a different type for consumption, but the vast majority use case has TWrite and TRead being the same, which is why the majority use happens via the derived Channel type, which is nothing more than:

```csharp 
public abstract class Channel<T> : Channel<T, T> { }
```
The non-generic Channel type then provides factories for several implementations of Channel<T>:

```csharp
public static class Channel
{
    public static Channel<T> CreateUnbounded<T>();
    public static Channel<T> CreateUnbounded<T>(UnboundedChannelOptions options);

    public static Channel<T> CreateBounded<T>(int capacity);
    public static Channel<T> CreateBounded<T>(BoundedChannelOptions options);
}
```

### Creating a channel

To create a channel, we can use the static Channel class which exposes factory methods to create the two main types of channel. 

#### Bounded Channel

`CreateBounded<T>` creates a channel with a finite capacity. In this scenario, it’s possible to develop a producer/consumer pattern which accommodates this limit. For example, you can have your producer await (non-blocking) capacity within the channel before it completes its write operation. This is a form of backpressure, which, when used, can slow your producer down, or even stop it, until the consumer has read some items and created capacity.

When creating a bounded channel, you can pass the capacity directly or you can define the `BoundedChannelOptions`, this options provide control over the behavior of this bounded channel.

```csharp
Channel<string> boundedChannel = Channel.CreateBounded<string>(10);
```
``` csharp
Channel<string> boundedChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(10)
{
    AllowSynchronousContinuations = false,
    FullMode = BoundedChannelFullMode.DropOldest,
    SingleReader = false,
    SingleWriter = true,
}); 
```

|Name | Value | Description |
|:-----| :----:| :----------- |
|AllowSynchronousContinuations | bool | `true` if operations performed on a channel may synchronously invoke continuations subscribed to notifications of pending async operations; `false` if all continuations should be invoked asynchronously.
|FullMode | Enum |Gets or sets the behavior incurred by write operations when the channel is full.
|SingleReader | bool | `true` readers from the channel guarantee that there will only ever be at most one read operation at a time; `false` if no such constraint is guaranteed.
|SingleWriter | bool | `true` if writers to the channel guarantee that there will only ever be at most one write operation at a time; `false` if no such constraint is guaranteed.

#### Unbounded Channel

`CreateUnbounded<T>` creates a channel with an unlimited capacity. This can be quite dangerous if your producer outpaces you the consumer. In that scenario, without a capacity limit, the channel will keep accepting new items. When the consumer is not keeping up, the number of queued items will keep increasing. Each item being held in the channel requires some memory which can’t be released until the object has been consumed. Therefore, it’s possible to run out of available memory in this scenario.

```csharp
Channel<string> unboundedChannel = Channel.CreateUnbounded<string>();
```
```csharp
Channel<string> unboundedChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
{
    AllowSynchronousContinuations = true,
    SingleReader = false,
    SingleWriter = true
});
```

|Name | Value | Description |
|:-----| :----:| :----------- |
|AllowSynchronousContinuations | bool | `true` if operations performed on a channel may synchronously invoke continuations subscribed to notifications of pending async operations; `false` if all continuations should be invoked asynchronously.
|SingleReader | bool | `true` readers from the channel guarantee that there will only ever be at most one read operation at a time; `false` if no such constraint is guaranteed.
|SingleWriter | bool | `true` if writers to the channel guarantee that there will only ever be at most one write operation at a time; `false` if no such constraint is guaranteed.


**Important** 

Notice that, the asynchronous execution ensures, that a producer thread does not end up doing consumer work when executing a continuation synchronously. If you are sure, that this added safety is not necessary in your specific use case, it can be turned off using another boolean property in the Channels options, which defaults to false. Doing so most likely increases throughput but reduces concurrency.

### Interacting with Channels

To interact with a channel, there are two features available.The ChannelWriter that can be used to write (publish) objects to the Channel and the ChannelReader that can be used to read (consume) objects.

| ChannelWriter | ChannelReader |
|:--------------| :-------------|
|bool TryWrite(T item)|bool TryRead(out T item)|
|ValueTask WriteAsync(T item)|ValueTask<T> ReadAsync()|
|ValueTask<bool> WaitToWriteAsync()|ValueTask<bool> WaitToReadAsync()|
|bool TryComplete(), void Complete()|Task Completion|

#### Writing

- Synchronously
```csharp
    Channel<string> ch = Channel.CreateUnbounded<string>();

    ch.Writer.TryWrite("Hi!");

    ch.Writer.Complete();
    
```
- Asynchronously

```csharp
    Channel<string> ch = Channel.CreateUnbounded<string>();

    await ch.Writer.WriteAsync("Hi!");

    ch.Writer.Complete();
```
**Important** 

`WaitToWriteAsync()` allow you to wait asynchronously until the Channel becomes writable again. Note, that there is no guarantee, that the channel will stay writable, until you acutally write to it.

Consider the following example:

```csharp
while (await ch.Writer.WaitToWriteAsync())
{
    if (ch.Writer.TryWrite("item"))
        return;
}
```

There are few good reasons why it’s using `WaitToWriteAsync()` in a loop. One is because different Producers might be sharing the Channel, so `WaitToWriteAsync()` could signal that we can proceed with writing, but then `TryWrite()` fails. This will put us back in the loop, awaiting for the next chance.


#### Reading

- Synchronously
```csharp
    Channel<string> ch = Channel.CreateUnbounded<string>();

    ch.Reader.TryRead(out string item);
    
```
- Asynchronously

```csharp
    Channel<string> ch = Channel.CreateUnbounded<string>();

    string item = await ch.Reader.ReadAsync();
```
**Important** 

`WaitToReadAsync()` allow you to wait asynchronously until the Channel becomes readable again. Note, that there is no guarantee, that the channel will stay readable, until you acutally read from it.

Consider the following example:

```csharp
while (await ch.Reader.WaitToReadAsync())
{
    if (ch.Reader.TryRead(out string item))
    {
        // process item...
    }
}
```



#### Merge/Multiplexer

In a scenario, where you have more than one producer and one consumer, the solution is this concept, the merge, basically, it will read from all the channels and it will write does items into a new channel then the consumer will have all the messages aggregated in one place.

 ```csharp
 
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

 ```

Consider the following example:

 ```csharp
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
 ```


#### Split/Demultiplexer

In a scenario, where you have one producer and more than one consumer, the solution is this concept, the split will create a channel for each consumer.

 ```csharp
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

 ```

Consider the following example:

 ```csharp
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
 ```


#### Channels with reflection

In a scenario where it's necessary to create channels generically, reflection might be a way to solve your problem consider the following code:

```csharp
var ch = await chGenerator.CreateChannel("System.String") as Channel<string>;
```

- ChannelGenerator, is a component that creates channels based on a given type. It supports `Bounded Channel Options` and `Unbounded Channel Options`.


#### Ref's
- [Working with Channels in .NET](https://www.youtube.com/watch?v=gT06qvQLtJ0)
- [An introduction to System.Threading.Channels](https://www.stevejgordon.co.uk/an-introduction-to-system-threading-channels)
- [C# Channels - Publish / Subscribe Workflows](https://deniskyashif.com/2019/12/08/csharp-channels-part-1/)
- [C# Channels - Timeout and Cancellation](https://deniskyashif.com/2019/12/11/csharp-channels-part-2/)
- [C# Channels - Async Data Pipelines](https://deniskyashif.com/2020/01/07/csharp-channels-part-3/)
- [Exploring System.Threading.Channels](https://ndportmann.com/system-threading-channels/)
