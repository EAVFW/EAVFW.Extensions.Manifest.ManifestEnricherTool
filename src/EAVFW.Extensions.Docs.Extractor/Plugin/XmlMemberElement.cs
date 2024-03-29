using System.Xml.Serialization;

namespace EAVFW.Extensions.Docs.Extractor
{
    [XmlRoot("member")]
    public class XmlMemberElement
    {
        [XmlAttribute("name")] public string Name { get; set; }

        [XmlElement("summary")] public string Summary { get; set; }
    }
}
