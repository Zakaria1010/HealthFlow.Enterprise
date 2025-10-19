using HealthFlow.Shared.Models;
using System.Threading.Channels;
using RabbitMQ.Client;


namespace HealthFlow.BackgroundWorker.Services;
public class PatientProcessingChannel
{
    private readonly Channel<PatientMessage> _channel;
    private readonly ILogger<PatientProcessingChannel> _logger;

    public PatientProcessingChannel(ILogger<PatientProcessingChannel> logger)
    {
        _logger = logger;
        
        // Create bounded channel with backpressure
        var options = new BoundedChannelOptions(1000)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        };
        
        _channel = Channel.CreateBounded<PatientMessage>(options);
    }

    public async Task WriteAsync(PatientMessage message, CancellationToken cancellationToken = default)
    {
        await _channel.Writer.WriteAsync(message, cancellationToken);
        _logger.LogDebug("Message queued: {MessageId}", message.Id);
    }

    public IAsyncEnumerable<PatientMessage> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}