using System;
using System.Collections.Generic;
using System.Linq;
using Sprache;

namespace EAVFW.Extensions.Docs.Generator
{
    public class TransformXmlTag
    {
        private Parser<string>? _linkReplace;

        private readonly Parser<string>? _toMarkDownLink;

        private readonly Parser<Dictionary<string, string>>? _properties;

        public TransformXmlTag()
        {
            var text =
                Parse.CharExcept('<').Many().Text();

            var property =
                from key in Parse.LetterOrDigit.Or(Parse.Char('-')).Many().Text()
                from eq in Parse.Char('=')
                from value in Parse.AnyChar.Except(Parse.Char('\"')).Many()
                    .Contained(Parse.Char('\"'), Parse.Char('\"'))
                    .Text()
                select new KeyValuePair<string, string>(key, value);

            _properties =
                from properties in property.DelimitedBy(Parse.Char(' '))
                select properties.ToDictionary(x => x.Key, x => x.Value);

            _toMarkDownLink =
                from s in text.Or(Parse.Ref(() => _linkReplace)).Many()
                select string.Join("", s);
        }

        /// <summary>
        /// Transform a string with Xml tags using the tag string to locate the 
        /// </summary>
        /// <param name="input">Input to transform</param>
        /// <param name="transform">Transform function with access to the properties</param>
        /// <returns></returns>
        public string TransformString(string input, Func<string, Dictionary<string, string>, string> transform)
        {
            _linkReplace =
                from s in Parse.String("<")
                from tag in Parse.LetterOrDigit.Many().Text()
                from _ in Parse.WhiteSpace.Many()
                from props in _properties
                from __ in Parse.WhiteSpace.Many()
                from e in Parse.String("/>")
                select transform(tag, props);

            return _toMarkDownLink.Parse(input);
        }
    }
}
