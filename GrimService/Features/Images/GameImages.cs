using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace GrimService.Features.Images
{
    public enum ImageFileKind { Other, Jpeg, Png }
    public readonly record struct ImageInspection(int Width, int Height, ImageFileKind Kind);
    public sealed class InvalidGameImageException : ArgumentException
    {
        public InvalidGameImageException() : base("Invalid or unsupported image data.") { }
    }

    public static class GameImages
    {
        public static ImageInspection Inspect(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new InvalidGameImageException();
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            return Inspect(codec);
        }

        public static async Task<ImageInspection> InspectAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var bytes = new MemoryStream();
            await stream.CopyToAsync(bytes, cancellationToken);
            return Inspect(bytes.ToArray());
        }

        public static byte[] PrepareNewsJpeg(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) throw new InvalidGameImageException();
            using var data = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(data);
            var info = Inspect(codec);
            if (info.Kind is not (ImageFileKind.Jpeg or ImageFileKind.Png) || info.Width > 4096 || info.Height > 4096 ||
                (long)info.Width * info.Height > 8_000_000)
                throw new InvalidGameImageException();

            using var pixels = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(pixels.Info, pixels.GetPixels()) != SKCodecResult.Success)
                throw new InvalidGameImageException();

            var origin = codec.EncodedOrigin;
            bool swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
            int width = swap ? info.Height : info.Width;
            int height = swap ? info.Width : info.Height;
            double scale = Math.Min(1280d / width, 720d / height);
            int targetWidth = Math.Clamp((int)Math.Round(width * scale), 1, 1280);
            int targetHeight = Math.Clamp((int)Math.Round(height * scale), 1, 720);
            using var output = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, SKColorType.Rgba8888, SKAlphaType.Opaque));
            using (var canvas = new SKCanvas(output))
            {
                canvas.Clear(SKColors.Black);
                canvas.Scale((float)targetWidth / width, (float)targetHeight / height);
                switch (origin)
                {
                    case SKEncodedOrigin.TopRight: canvas.Translate(width, 0); canvas.Scale(-1, 1); break;
                    case SKEncodedOrigin.BottomRight: canvas.Translate(width, height); canvas.RotateDegrees(180); break;
                    case SKEncodedOrigin.BottomLeft: canvas.Translate(0, height); canvas.Scale(1, -1); break;
                    case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                    case SKEncodedOrigin.RightTop: canvas.Translate(width, 0); canvas.RotateDegrees(90); break;
                    case SKEncodedOrigin.RightBottom: canvas.Translate(width, height); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                    case SKEncodedOrigin.LeftBottom: canvas.Translate(0, height); canvas.RotateDegrees(270); break;
                }
                using var image = SKImage.FromBitmap(pixels);
                canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKCubicResampler.Mitchell));
            }
            using var result = SKImage.FromBitmap(output);
            using var jpeg = result.Encode(SKEncodedImageFormat.Jpeg, 85);
            return jpeg?.ToArray() ?? throw new InvalidGameImageException();
        }

        private static ImageInspection Inspect(SKCodec codec)
        {
            if (codec == null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
                throw new InvalidGameImageException();
            var kind = codec.EncodedFormat switch {
                SKEncodedImageFormat.Jpeg => ImageFileKind.Jpeg,
                SKEncodedImageFormat.Png => ImageFileKind.Png,
                _ => ImageFileKind.Other
            };
            return new(codec.Info.Width, codec.Info.Height, kind);
        }
    }
}
