using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.DependencyInjection;

namespace GrimService.Infrastructure.Protocol
{
    public static class GameXmlProtocol
    {
        public static IServiceCollection AddGameXmlProtocol(this IServiceCollection services)
        {
            services.AddMvc(options => {
                if (!options.OutputFormatters.OfType<GameXmlFormatter>().Any())
                    options.OutputFormatters.Add(new GameXmlFormatter());
            }).AddXmlSerializerFormatters();
            return services;
        }

        internal static XmlWriterSettings WriterSettings() => new()
        {
            Indent = true,
            IndentChars = string.Empty,
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Replace,
            OmitXmlDeclaration = false
        };
    }

    public sealed class GameXmlFormatter : XmlSerializerOutputFormatter
    {
        public GameXmlFormatter() : base(GameXmlProtocol.WriterSettings()) { }

        protected override void Serialize(XmlSerializer serializer, XmlWriter writer, object value)
        {
            var namespaces = new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });
            serializer.Serialize(writer, value, namespaces);
        }
    }
}
