using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace GrimService.Features.Resources
{
    public sealed class GameResourceStore
    {
        private readonly string root;
        public GameResourceStore(string root) => this.root = Path.GetFullPath(root);

        public async Task CopyAsync(HttpContext context, string relativePath)
        {
            string file;
            try
            {
                if (string.IsNullOrEmpty(relativePath) || Path.IsPathRooted(relativePath))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                file = Path.GetFullPath(Path.Combine(root, relativePath));
                string withinRoot = Path.GetRelativePath(root, file);
                if (Path.IsPathRooted(withinRoot) || withinRoot == ".." ||
                    withinRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                if (!File.Exists(file))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }
                string current = root;
                foreach (string segment in withinRoot.Split(Path.DirectorySeparatorChar))
                {
                    current = Path.Combine(current, segment);
                    if (new FileInfo(current).LinkTarget != null)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }
                }
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            catch (FileNotFoundException) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            catch (DirectoryNotFoundException) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }

            FileStream input;
            try
            {
                input = new FileStream(file, new FileStreamOptions {
                    Access = FileAccess.Read, Mode = FileMode.Open, Share = FileShare.Read,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                });
            }
            catch (FileNotFoundException) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            catch (DirectoryNotFoundException) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }

            await using (input)
                await input.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
    }
}
