using CommandLine;

namespace MMMScanner
{
    public class Options
    {
        [Option('s', "source", Required = true, HelpText = "The solution path.")]
        public string Source { get; set; }

        [Option('d', "destination", Required = false, HelpText = "The file path of the output json file. default is 'key.json' relative to the solution directory")]
        public string Destination { get; set; } = "mmm.json";
    }
}