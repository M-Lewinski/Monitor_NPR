using CommandLine;

namespace Monitor
{
	public interface IOptionsInit
	{
		[Option('m',"main", Required = true, SetName = "MainNodeIndicator", HelpText = "Running process as main node for initialization.")]
		bool MainNode { get; set; }

		[Option('c',"config", Required = true, HelpText = "Configuration file name. File should contain information about remote addresses.")]
		string FileName { get; set; }
		
		[Option('n', "number", Required = true, SetName = "AddressIndicator", HelpText = "Address number used for communication with others (taken from file)")]
		int? Number { get; set; }

	}
}