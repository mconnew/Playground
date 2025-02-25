using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace BinaryXmlSerialization
{
    internal class TrackingXmlBinaryWriterSession : XmlBinaryWriterSession
    {
        private List<XmlDictionaryString> _newStrings;

        public bool HasNewStrings
        {
            get { return _newStrings != null && _newStrings.Count > 0; }
        }

        public IList<XmlDictionaryString> NewStrings => _newStrings;

        public void ClearNew()
        {
            _newStrings.Clear();
        }

        public override bool TryAdd(XmlDictionaryString value, out int key)
        {
            if (base.TryAdd(value, out key))
            {
                if (_newStrings == null)
                {
                    _newStrings = new List<XmlDictionaryString>();
                }

                _newStrings.Add(value);
                return true;
            }

            return false;
        }
    }
}
