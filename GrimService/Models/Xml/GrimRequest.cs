using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace GrimService.Models.Xml
{
    [XmlRoot("grim")]
    public class GrimRequest
    {
        private static readonly XmlSerializer Serializer = new(typeof(GrimRequest));

        [XmlElement("command")]
        public string Command { get; set; }
        [XmlElement("params")]
        public GrimRequestParams Params { get; set; }

        public static Task<GrimRequest> Deserialize(Stream input) => Deserialize(input, CancellationToken.None);

        public static async Task<GrimRequest> Deserialize(Stream input, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(input);
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            using var reader = XmlReader.Create(buffer, ReaderSettings());
            return Serializer.Deserialize(reader) as GrimRequest;
        }

        public static GrimRequest Deserialize(string inputXml)
        {
            using var text = new StringReader(inputXml);
            using var reader = XmlReader.Create(text, ReaderSettings());
            return Serializer.Deserialize(reader) as GrimRequest;
        }

        private static XmlReaderSettings ReaderSettings() => new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = false
        };

        public bool TryGetParameterByIndex(int index, out GrimRequestParam param)
        {
            var list = Params?.ParamList;
            param = list != null && index >= 0 && index < list.Count ? list[index] : null;
            return param != null;
        }

        public bool TryGetParameterByKey(string key, out GrimRequestParam param)
        {
            if (Params?.ParamList != null)
                foreach (var candidate in Params.ParamList)
                    if (candidate != null && candidate.Key == key)
                    {
                        candidate.Text ??= string.Empty;
                        param = candidate;
                        return true;
                    }

            param = null;
            return false;
        }

        public bool TryGetParameterIntByKey(string key, out int value)
        {
            value = 0;
            return TryGetParameterByKey(key, out var param) && int.TryParse(param.Text, out value);
        }

        public bool TryGetParameterLongByKey(string key, out long value)
        {
            value = 0;
            return TryGetParameterByKey(key, out var param) && long.TryParse(param.Text, out value);
        }
    }

    [XmlRoot("params")]
    public class GrimRequestParams
    {
        [XmlElement("param")]
        public List<GrimRequestParam> ParamList { get; set; }
    }

    [XmlRoot("param")]
    public class GrimRequestParam
    {
        [XmlAttribute("key")]
        public string Key { get; set; }
        [XmlText]
        public string Text { get; set; }
    }
}
