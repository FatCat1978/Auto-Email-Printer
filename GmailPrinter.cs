// Copyright 2018 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

// [START gmail_quickstart]
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Thread = System.Threading.Thread;

namespace GmailPrinter
{

	//TODO:
	//Set this on a loop - check every five minutes?
	//sanity checking. error printing?
	//figure out how to make the API key permanant

	//todo:
	//key expiration sanity checking. delete the credentials.json if we can't log in.	


	class GmailPrinter
	{
		private static bool debugMode = true; //if true, prevents printing and setting emails as read.
		private static Font printFont = new Font("Consolas", 12);
		private static StreamReader streamToPrint;
		// If modifying these scopes, delete your previously saved credentials
		// at ~/.credentials/gmail-dotnet-quickstart.json
		static string[] Scopes = { GmailService.Scope.GmailModify };
		static string ApplicationName = "AutoGmailPrinter";
		static GmailService service;

		static IList<MessagePart> xd;



		static void Main(string[] args)
		{
			UserCredential credential;

			using (var stream =
				new FileStream("credentials.json", FileMode.Open, FileAccess.Read))
			{
				// The file token.json stores the user's access and refresh tokens, and is created
				// automatically when the authorization flow completes for the first time.
				string credPath = "token.json";
				credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
					GoogleClientSecrets.FromStream(stream).Secrets,
					Scopes,
					"user",
					CancellationToken.None,
					new FileDataStore(credPath, true)).Result;
				Console.WriteLine("Credential file saved to: " + credPath);
			}

			// Create Gmail API service.
			service = new GmailService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credential,
				ApplicationName = ApplicationName,
			});

			// Define parameters of request.
			//    UsersResource.MessagesResource.ListRequest request = service.Users.Messages.List("me", "is:unread");

			getUnreadEmails(service);
			System.Timers.Timer timer = new System.Timers.Timer(TimeSpan.FromMinutes(.25).TotalMilliseconds);
			timer.AutoReset = true;
			timer.Elapsed += new ElapsedEventHandler(EmailCaller);
			timer.Start();
			while(true) //horrible!
			{ 
				string typed = Console.ReadLine(); // Prevents program from exiting.
				if (typed == "exit" || typed == "x")
					return;
			}
		}


		public static void EmailCaller(object sender, ElapsedEventArgs e)
		{
			Console.WriteLine("checking for new mail...");
			getUnreadEmails(service);
		}

		// Get UNREAD messages
		public static void getUnreadEmails(GmailService service)
		{
			UsersResource.MessagesResource.ListRequest Req_messages = service.Users.Messages.List("me");
			// Filter by labels
			Req_messages.LabelIds = new List<String>() { "INBOX", "UNREAD" };
			// Get message list
			IList<Message> messages = Req_messages.Execute().Messages;
			if ((messages != null) && (messages.Count > 0))
			{
				foreach (Message List_msg in messages)
				{
					// Get message content
					UsersResource.MessagesResource.GetRequest MsgReq = service.Users.Messages.Get("me", List_msg.Id);
					ModifyMessageRequest mods = new ModifyMessageRequest();
					mods.RemoveLabelIds = new List<String>();
					mods.RemoveLabelIds.Add("UNREAD");
					mods.RemoveLabelIds.Add("INBOX");
					Message msg = MsgReq.Execute();

					SendToPrinter(msg);
					//and set it as read, so it doesn't print twice.
					if(!debugMode)
						service.Users.Messages.Modify(mods, "me", List_msg.Id).Execute();
				}

			}
		}

		private static void SendToPrinter(Message msg)
		{

			String to = "";
			String from = "";
			String date = "";
			string subject = "";


			foreach (var x in msg.Payload.Headers) //there's probably a better way to do this. too bad!
			{
				//Console.WriteLine(x.Name);

				if (x.Name == "To")
					to = x.Value;
				if (x.Name == "From")
					from = x.Value;
				if (x.Name == "Date")
					date = x.Value;
				if (x.Name == "Subject")
					subject = x.Value;

				//	Console.WriteLine(x.Name + x.Value);
				//Console.WriteLine();
			}
			Console.WriteLine();

			String EmailSentTo = "To: " + to; //to


			String EmailSentBy = "From: " + from; //from
			String EmailSentDate = "Date: " + date; //date
			String EmailSubject = "Subject: " + subject; //subject

			xd = msg.Payload.Parts;

			String bodyText = msg.Payload.Parts[0].Body.Data;

			try { 
			if(bodyText != null)
				{ 
					byte[] data = Convert.FromBase64String(bodyText);
				bodyText = Encoding.UTF8.GetString(data);
				}

			else
				{
					bodyText = "WARNING: There was no message content associated with this email. this is likely an error.";
				}

			}
		
			
			catch (Exception e)
			{
				Console.WriteLine(e.Message);
				bodyText = e.Message;
			}
			string[] toPrintLines =
				{ EmailSentTo, EmailSentBy, EmailSentDate, EmailSubject, "-=-=-=-=-=-=-=-=-=-=-=-=-=-=-", bodyText};


			foreach (var line in toPrintLines)
			{
				Console.WriteLine(line);
			}
			Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++");


			File.WriteAllLines("EmailTemp.txt", toPrintLines);

			if(!debugMode)
				PrintWrittenFile();

		}

		private static void PrintWrittenFile()
		{
			PrintDocument printDocument1 = new PrintDocument();
			printDocument1.DefaultPageSettings.Margins = new Margins(0,0,0,0);
			printDocument1.PrintPage += new PrintPageEventHandler(printDocument1_PrintPage);
			printDocument1.Print();



		}
		private static void printDocument1_PrintPage(object sender, PrintPageEventArgs e)
		{
			String toPrint = "";

			using (FileStream stream = new FileStream("EmailTemp.txt", FileMode.Open))
			using (StreamReader reader = new StreamReader(stream))
			{
				toPrint = reader.ReadToEnd();
			}

			int charactersOnPage = 0;
			int linesPerPage = 0;

			// Sets the value of charactersOnPage to the number of characters
			// of stringToPrint that will fit within the bounds of the page.
			e.Graphics.MeasureString(toPrint, printFont,e.MarginBounds.Size, StringFormat.GenericTypographic,out charactersOnPage, out linesPerPage);

			// Draws the string within the bounds of the page
			e.Graphics.DrawString(toPrint, printFont, Brushes.Black,e.MarginBounds, StringFormat.GenericTypographic);

			// Remove the portion of the string that has been printed.
			toPrint = toPrint.Substring(charactersOnPage);

			// Check to see if more pages are to be printed.
			e.HasMorePages = (toPrint.Length > 0);

		}
	}
}
