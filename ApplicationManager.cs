using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace PriorityEmailApp
{
	static class ApplicationManager
	{
		//Info
		private static DateTime _initializationTime = new DateTime().ToLocalTime(); //when we started up. Mostly used for uptime tracking.
		private static DateTime _lastNewEmail; //how long ago was the last email we got?
		private static DateTime _lastException; //When was the last error we got? the ID of the email itself (if applicable) that caused the error is logged by the LoggingAgent.
		private static int _totalExceptions = 0; //how many errors we've gotten.


		//the classes we need to work.
		public static ConfigurationLoader ConfigLoader = new ConfigurationLoader();
		public static LoggingAgent logger = new LoggingAgent();
		public static GmailPrinter gmailPrinter = new();


		static void Main(string[] args)
		{
			System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromMinutes(.5).TotalMilliseconds);
			timer.AutoReset = true;
			timer.Elapsed += new ElapsedEventHandler(AsyncUpdate); //is it actually Async? Nobody knows!
			timer.Start();
			while (true) //horrible! figure out a better way of doing this.
			{
				string typed = Console.ReadLine(); // Prevents program from exiting.
				if (typed == "exit" || typed == "x")
					return;
			}
		}

		public static void AsyncUpdate(object sender, ElapsedEventArgs e)
		{
			UpdateConsoleUI();
			gmailPrinter.getUnreadEmails();
			Console.WriteLine("checking for new mail...");
			
		}

		private static void UpdateConsoleUI()
		{
			return;
		}
	}
}
