using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text.RegularExpressions;

namespace DO_Ranking_dumper
{
    class Program
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PrintError(string Err)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Error] {Err}");
        }

        public static void Main(string[] args)
        {
            /*
             Target usage: DO_Dumper [SERVER] [DOSID]
             */
            if (args.Length != 2 || args[1].Length != 32)
            {
                PrintError("Wrong args!");
                return;
            }

            HTTPMgr HTTP = new HTTPMgr();
            TimeSpan Elapsed;

            // Login into the game with SID.    
            {
                string HTML;
                if (!HTTP.GET($"http://{args[0]}.darkorbit.bigpoint.com/indexInternal.es?action=internalHallofFame&view=User&dosid={args[1]}", out HTML)) { PrintError("Failed to get first website!"); return; }

                if (!HTML.Contains("p://darkorbit-22.ah.bpcdn.net/css/cdn/internalHallofFame.css"))
                {
                    PrintError("Failed to load ranks site, maybe wrong sid?");
                    return;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Logged in with SID!");

            string OutputPath = $"{Directory.GetCurrentDirectory()}\\dump_{args[0]}.sql";
            if (File.Exists(OutputPath))
                File.Delete(OutputPath);

            UTF8Encoding Encoder = new UTF8Encoding(false);
            DateTime StartTime = DateTime.Now;

            using (FileStream FS = new FileStream(OutputPath, FileMode.CreateNew))
            {
                // Write credits header
                {
                    string Header = $"-- Exported with DO_Dumper by NoobKillerPL\n-- Generated on {String.Format("{0:dd/MM/yyyy HH:mm:ss}", StartTime)}\n";
                    byte[] Bytes = Encoder.GetBytes(Header);
                    FS.Write(Bytes, 0, Bytes.Length);
                }

                // Write SQL header
                {
                    byte[] SQL_Header = Encoder.GetBytes(@"
CREATE TABLE `players` (
  `position` int(11) NOT NULL,
  `name` varchar(255) NOT NULL,
  `rank` varchar(64) NOT NULL,
  `points` int(11) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

SET ESCAPE '\'
");
                    FS.Write(SQL_Header, 0, SQL_Header.Length);
                }

                // 100 pages of ranking...
                Parallel.For(1, 101,
                    new ParallelOptions { MaxDegreeOfParallelism = 4 },
                    (i, loopState) =>
                    {
                    LB_TRY_AGAIN:

                        string HTML;
                        if (!HTTP.GET($"http://{args[0]}.darkorbit.bigpoint.com/indexInternal.es?action=internalHallofFame&view=User&dps={i}", out HTML))
                        {
                            PrintError($"Failed to load {i} page!");
                            return;
                        }

                        if (!HTML.Contains("p://darkorbit-22.ah.bpcdn.net/css/cdn/internalHallofFame.css"))
                        {
                            PrintError($"Failed to page #{i}! We'll try again in 4s...");
                            Thread.Sleep(4000);
                            goto LB_TRY_AGAIN;
                        }

                        //Console.ForegroundColor = ConsoleColor.DarkGray;
                        //Console.WriteLine($"Loaded {i}!");

                        // trim beginning & end (useless info)
                        HTML = HTML.Substring(HTML.IndexOf("hof_ranking_table defaultRanking") + "hof_ranking_table defaultRanking".Length);
                        HTML = HTML.Substring(HTML.IndexOf("<tr>"));
                        HTML = HTML.Substring(0, HTML.IndexOf("</table>"));

                        int Accs = 0;
                        MatchCollection mc = Regex.Matches(HTML, @"<tr>\s*(.+?)</tr>", RegexOptions.Singleline);

                        foreach (Match _Match in mc)
                        {
                            string AccData = _Match.Value;

                            if (AccData.Contains("colspan"))
                                continue;   //It's the separator entry

                            string Position, Name, Rank, Points;
                            MatchCollection Matches;

                            Matches = Regex.Matches(AccData, "56px;\">(.+?)</td>", RegexOptions.Singleline);
                            if (Matches.Count != 1)
                                throw new Exception("Failed to parse rank number with regex!");
                            Position = Matches[0].Groups[1].Value;

                            Matches = Regex.Matches(AccData, "title=\"(.+?)\"", RegexOptions.Singleline);
                            if (Matches.Count != 1)
                                throw new Exception("Failed to parse nickname with regex!");
                            Name = Matches[0].Groups[1].Value;

                            Matches = Regex.Matches(AccData, "6b;\">(.+?)</td>", RegexOptions.Singleline);
                            if (Matches.Count != 1)
                                throw new Exception("Failed to parse rank of the player with regex!");
                            Rank = Matches[0].Groups[1].Value;

                            Matches = Regex.Matches(AccData, "30px;\">\\s*(.+?)\\s*</td>", RegexOptions.Singleline);
                            if (Matches.Count != 1)
                                throw new Exception("Failed to parse rank of the player with regex!");
                            Points = Matches[0].Groups[1].Value.Replace(",", "");

                            // Fix Name for bad characters
                            Name = Name.Replace("'", "");
                            Name = Name.Replace("\"", "");
                            Name = Name.Replace(";", "");
                            Name = Name.Replace("[", "\\[");
                            Name = Name.Replace("]", "\\]");

                            // Write a SQL string to file
                            string SQL = $"{Environment.NewLine}INSERT INTO `players` (`position`, `name`, `rank`, `points`) VALUES ('{Position}', '{Name}', '{Rank}', '{Points}');";

                            byte[] SQL_Bytes = Encoder.GetBytes(SQL);
                            FS.Write(SQL_Bytes, 0, SQL_Bytes.Length);

                            Accs++;
                        }

                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine($"Parsed whole {i} page with {Accs} accs on it!");
                    });
                
                Elapsed = DateTime.Now - StartTime;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\nDumped all players from main ranking table in {Elapsed} time!");

                /*
                    Some notes for later:
                    - All honor & destroyed aliens / ships count have 100 pages limit.
                    - Experience on the other hand for some reason has >600 pages on int1.
                    - Ranks are often in different languages, would be cool to make a dictionary from all servers... #TODO
                */

                // Experience?
                
            }

            Elapsed = DateTime.Now - StartTime;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nExported results to 'dump_{args[0]}.sql' file in {Elapsed} time!");

            Console.ReadKey();
        }
    }
}
