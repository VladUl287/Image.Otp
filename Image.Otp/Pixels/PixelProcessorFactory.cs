using Image.Otp.Abstractions;
using Image.Otp.Core.Primitives;
using System.Collections.Frozen;

namespace Image.Otp.Core.Pixels;

public static class PixelProcessorFactory
{
    private static FrozenDictionary<Type, object> _processors =
        new Dictionary<Type, object>()
        {
            { typeof(Rgba32), new Rgba32Processor() },
            { typeof(Rgb24), new Rgb24Processor() }
        }
        .ToFrozenDictionary();

    public static IPixelProcessor<T> GetProcessor<T>() where T : unmanaged, IPixel<T>
    {
        if (_processors.TryGetValue(typeof(T), out var processor))
        {
            return (IPixelProcessor<T>)processor;
        }

        throw new NotSupportedException($"Pixel format {typeof(T).Name} is not supported");
    }

    private static readonly Lock _updateLock = new();
    public static void RegisterProcessor<TPixel>(IPixelProcessor<TPixel> processor) where TPixel : unmanaged, IPixel<TPixel>
    {
        lock (_updateLock)
        {
            var newDict = _processors.ToDictionary();
            newDict[typeof(TPixel)] = processor;
            _processors = newDict.ToFrozenDictionary();
        }
    }

    public static void RegisterProcessors(params (Type PixelType, object Processor)[] processors)
    {
        ArgumentNullException.ThrowIfNull(processors);

        if (processors.Length == 0)
            return;

        lock (_updateLock)
        {
            var newDict = _processors.ToDictionary();

            foreach (var (pixelType, processor) in processors)
            {
                // Validate null values
                if (pixelType is null)
                    throw new ArgumentException("PixelType cannot be null", nameof(processors));

                if (processor is null)
                    throw new ArgumentException($"Processor for type {pixelType.Name} cannot be null", nameof(processors));

                // Validate that the pixel type implements IPixel<T>
                if (!IsValidPixelType(pixelType))
                    throw new ArgumentException($"Type {pixelType.Name} does not implement IPixel<>", nameof(processors));

                // Validate that the processor implements IPixelProcessor<T> for the correct T
                if (!IsValidProcessorForPixelType(processor, pixelType))
                    throw new ArgumentException($"Processor {processor.GetType().Name} is not compatible with pixel type {pixelType.Name}", nameof(processors));

                newDict[pixelType] = processor;
            }

            _processors = newDict.ToFrozenDictionary();
        }
    }

    // Helper method to validate pixel type implements IPixel<T>
    private static bool IsValidPixelType(Type pixelType)
    {
        // Check if pixelType implements IPixel<T> where T is itself
        return pixelType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPixel<>))
            .Any(i => i.GetGenericArguments()[0] == pixelType);
    }

    // Helper method to validate processor implements IPixelProcessor<T> for the correct T
    private static bool IsValidProcessorForPixelType(object processor, Type pixelType)
    {
        var processorType = processor.GetType();

        // Check if processor implements IPixelProcessor<T> where T is the pixelType
        return processorType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IPixelProcessor<>))
            .Any(i => i.GetGenericArguments()[0] == pixelType);
    }
}
