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

using System.Diagnostics;

using System.Text;
using System.Text.RegularExpressions;
using System.Timers;

namespace PriorityEmailApp
{

//TODO:
//throw this entire class into a dedicated "MailHandler" Class. use this as a management class.
//track uptime, last email recieved, etc.
//logging! Mostly just for errors?
//Config!
//	Debugmode
//	CheckRate
//	Background Process?
//
//error fixes:
//Prompt the user to reset

	public class GmailPrinter
	{

		// If modifying these scopes, delete your previously saved credentials
		// at ~/.credentials/gmail-dotnet-quickstart.json
		static string[] Scopes = { GmailService.Scope.GmailModify };
		static string ApplicationName = "AutoGmailPrinter";
		static GmailService service;		
		static IList<MessagePart> payloadParts; //does this need to be here?

		public GmailPrinter()
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

		}


		// Get UNREAD messages
		public void getUnreadEmails()
		{
			UsersResource.MessagesResource.ListRequest Req_messages = service.Users.Messages.List("me");
			// Filter by labels
			Req_messages.LabelIds = new List<String>() { "INBOX", "UNREAD" };
			// Get message list
			IList<Message> messages = null;
			try
			{
				messages = Req_messages.Execute().Messages;
				if (messages != null)
					Console.WriteLine(messages.Count + " Messages found.");
				else
					Console.WriteLine("No Messages found.");
			}
			catch (Exception E)
			{
				Console.WriteLine("Error fetching messages! Internet issue?");
				Console.WriteLine(E.Message); //there's a bug regarding expiring credentials, hard to pinpoint what type it is, so I gotta sniff it out here
			}
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
					if(!ApplicationManager.ConfigLoader.debugMode)
						service.Users.Messages.Modify(mods, "me", List_msg.Id).Execute();
				}

			}
		}

		private static void SendToPrinter(Message msg) //rename this to sendMsgToFile or something intelligent sounding
		{
			
			String to = "";
			String from = "";
			String date = "";
			string subject = ""; //todo, make this a dict w/ a method to generate from payload.headers


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
			//todo, count attachments?

			payloadParts = msg.Payload.Parts;
			String bodyText = BodyTextFromPayloadParts(payloadParts);

			bodyText = SanitizeEmailBodyText(bodyText);


			if(bodyText == "No Message Attached.")
			{
				bodyText = "!Read Error! Snippet: " + msg.Snippet;
			}
		
			string[] toPrintLines =
				{ EmailSentTo, EmailSentBy, EmailSentDate, EmailSubject, "-=-=-=-=-=-=-=-=-=-=-=-=-=-=-", bodyText};


			foreach (var line in toPrintLines)
			{
				Console.WriteLine(line);
			}
			Console.WriteLine("++++++++++++++++++++++++++++++++++++++++++++++++");


			File.WriteAllLines("EmailTemp.txt", toPrintLines);

			if(!ApplicationManager.ConfigLoader.debugMode)
				PrintWrittenFile();

		}

		private static string SanitizeEmailBodyText(string bodyText)
		{
			return Regex.Replace(bodyText, @"(http|https):\/\/[\w\-_]+(\.[\w\-_]+)+[\w\-\.,@?^=%&amp;:\/~\+#]*[\w\-\@?^=%&amp;\/~\+#]", "<URL>"); //regex is a nightmare. 
		}

		private static string BodyTextFromPayloadParts(IList<MessagePart> payloadParts)
		{
			String bodyText = "No Message Attached.";
			try
			{
				if (payloadParts != null && payloadParts.Count > 0)
				{
					foreach (MessagePart part in payloadParts)
					{
						Console.WriteLine("Part MimeType:" + part.MimeType);
						if (part.MimeType == "text/plain") //"text/html"
						{
							byte[] data = FromBase64ForUrlString(part.Body.Data);
							string decodedString = Encoding.UTF8.GetString(data);
							Console.WriteLine(decodedString);
							bodyText = decodedString;
						}
						if(part.MimeType == "multipart/alternative")
						{
							return BodyTextFromPayloadParts(part.Parts); //this has to be a recursive method. well. "has."
						}
					}
				}

			}
			catch (Exception e)
			{
				//TODO: change this over to the loggingagent stuff make the bodytext a generic warning like "Could not parse the email, read it properly".
				Console.WriteLine(e.Message);
				bodyText = e.Message;
			}

			return bodyText;
		}

		public static byte[] FromBase64ForUrlString(string base64ForUrlInput) //thanks to stackoverflow. the original source is ambigous because I've seen it pop up in like 30 different places.
		{
			int padChars = (base64ForUrlInput.Length % 4) == 0 ? 0 : (4 - (base64ForUrlInput.Length % 4));
			StringBuilder result = new StringBuilder(base64ForUrlInput, base64ForUrlInput.Length + padChars);
			result.Append(String.Empty.PadRight(padChars, '='));
			result.Replace('-', '+');
			result.Replace('_', '/');
			return Convert.FromBase64String(result.ToString());
		}

		private static void PrintWrittenFile() //todo - replace with the "proper" print thing again? This method's pretty elegant.
		{
			var psi = new ProcessStartInfo("EmailTemp.txt");
			psi.UseShellExecute = true;
			psi.Verb = "print";
			psi.WindowStyle = ProcessWindowStyle.Hidden;
			psi.CreateNoWindow = true;
			var process = System.Diagnostics.Process.Start(psi);
		}
	}
}
