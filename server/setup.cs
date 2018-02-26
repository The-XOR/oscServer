using System.IO;

namespace server
{
	public class Setup
	{
		public string destAddress {get; set;}
		public string destPort {get; set;}
		public string listenPort {get; set;}

		private string getFn()
		{
			return Path.ChangeExtension(System.Reflection.Assembly.GetEntryAssembly().Location, ".xml");
		}
		public void Save()
		{			
			System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(GetType());
			using(TextWriter textWriter = new StreamWriter(getFn()))
			{
				serializer.Serialize(textWriter, this);
				textWriter.Close();
			}
		}

		public void Load()
		{
			string file = getFn();
			if(File.Exists(file))
			{
				System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(GetType());
				using(TextReader textReader = new StreamReader(file))
				{
					Setup s = (Setup)serializer.Deserialize(textReader);
					textReader.Close();
					destAddress = s.destAddress;
					destPort = s.destPort;
					listenPort = s.listenPort;
				}
			}
		}

	}
}
