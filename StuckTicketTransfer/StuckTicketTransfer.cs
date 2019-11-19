using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Xml;
using System.Threading;
using System.Net;

namespace StuckTicketTransfer
{
	class StuckTicketTransfer
	{
		//-----------------------------------
		// Declaring Private Variables
		//-----------------------------------
		private static int _transferCount = 0;

		static void Main(string[] args)
		{
			//-----------------------------------
			// Declaring Local Variables
			//-----------------------------------
			ITSD_WebServices.USD_WebService _wsClient = new ITSD_WebServices.USD_WebService();
			List<string> _TicketsToTransfer = new List<string>();
			string _groupHandle = "";
			string _groupHandleXML = "";
			string _assigneeHandle = "";
			string _assigneeHandleXML = "";
			string _TicketHandle = "";
			string _TicketHandleXML = "";
			int sid = 0;
			string userHandle = "";

			//-----------------------------------
			// Creating Application Log File
			//-----------------------------------
			FileStream fs_1;
			fs_1 = new FileStream(ConfigurationManager.AppSettings["ApplicationLog"] + "_" + DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString() + ".log", FileMode.Create, FileAccess.Write);
			StreamWriter _applicationLog = new StreamWriter(fs_1);

			XmlDocument _xmlDoc = new XmlDocument();
			XmlNodeList doSelectXML = null;

			//--------------------------------------
			// Reading the data from (Neville) view
			//--------------------------------------

			//Log Entry
			_applicationLog.WriteLine("-------------------------------------------------------------------------");
			_applicationLog.WriteLine(DateTime.Now + " |INFO| Reading the view data. ");
			_applicationLog.WriteLine("-------------------------------------------------------------------------");
			_applicationLog.Flush();

			_TicketsToTransfer = getViewData(_applicationLog);

			//Log Entry
			_applicationLog.WriteLine("-------------------------------------------------------------------------");
			_applicationLog.WriteLine("TOTAL [" + _TicketsToTransfer.Count + "]");
			_applicationLog.WriteLine("");
			_applicationLog.Flush();


			try
			{
				//-----------------------------------
				// Retrieving SID
				//-----------------------------------
				sid = getSID(ConfigurationManager.AppSettings["user"], ConfigurationManager.AppSettings["password"], _applicationLog, _wsClient);

				//-----------------------------------
				// Retrieving the user handle
				//-----------------------------------
				userHandle = getUserHandle(sid, ConfigurationManager.AppSettings["transfer_user"], _applicationLog, _wsClient);

				//Log Entry
				_applicationLog.WriteLine("-------------------------------------------------------------------------");
				_applicationLog.WriteLine(DateTime.Now + " |INFO| Transfer the Tickets ");
				_applicationLog.WriteLine("-------------------------------------------------------------------------");
				_applicationLog.Flush();

				for (int count = 0; count < _TicketsToTransfer.Count; count++)
				{
					//-----------------------------
					// Get the ticket handle
					//-----------------------------
					_transferCount = _TicketsToTransfer.Count;

					_TicketHandleXML = getTicketHandle(sid, _TicketsToTransfer[count].Split('|')[0], _wsClient, _applicationLog);

					_xmlDoc.LoadXml(_TicketHandleXML);

					doSelectXML = _xmlDoc.GetElementsByTagName("Handle");

					_TicketHandle = doSelectXML[0].InnerText;


					//-----------------------------
					// Get the group handle
					//-----------------------------
					_groupHandleXML = getGroupHandle(sid, _TicketsToTransfer[count].Split('|')[1], _wsClient, _applicationLog);

					_xmlDoc.LoadXml(_groupHandleXML);

					doSelectXML = _xmlDoc.GetElementsByTagName("Handle");

					_groupHandle = doSelectXML[0].InnerText;



					//-----------------------------
					// Get the new assignee handle (OPTIONAL)
					//-----------------------------

					if (!_TicketsToTransfer[count].Split('|')[2].Equals(""))
					{
						_assigneeHandleXML = getAssigneeHandle(sid, _TicketsToTransfer[count].Split('|')[2], _wsClient, _applicationLog);

						_xmlDoc.LoadXml(_assigneeHandleXML);

						doSelectXML = _xmlDoc.GetElementsByTagName("Handle");

						_assigneeHandle = doSelectXML[0].InnerText;

					}

					//-------------------------------
					// Transfer the Ticket
					//-------------------------------

					if (!_TicketsToTransfer[count].Split('|')[2].Equals(""))
					{
						_wsClient.transfer(sid, userHandle, _TicketHandle, _TicketsToTransfer[count].Split('|')[3], true, _assigneeHandle, true, _groupHandle, false, "");
					}
					else
					{
						_wsClient.transfer(sid, userHandle, _TicketHandle, _TicketsToTransfer[count].Split('|')[3], false, "", true, _groupHandle, false, "");
					}



					//Log Entry
					_applicationLog.WriteLine(_TicketsToTransfer[count].Split('|')[0] + "|Group: " + _groupHandle + "|Assignee: " + _assigneeHandle);
					_applicationLog.Flush();

					//-----------------------------
					// Wait 2 Seconds 
					//-----------------------------
					Thread.Sleep(Int32.Parse(ConfigurationManager.AppSettings["waitPeriod"]));

					//Log Entry
					_applicationLog.WriteLine("Wait [" + ConfigurationManager.AppSettings["waitPeriod"] + "] Milli Secs");
					_applicationLog.Flush();
				}

				//-----------------------------
				// Logout SID
				//-----------------------------

				//Log Entry
				_applicationLog.WriteLine("-------------------------------------------------------------------------");
				_applicationLog.WriteLine("Logout SID [" + sid + "]");
				_applicationLog.WriteLine("-------------------------------------------------------------------------");
				_applicationLog.Flush();

				_wsClient.logout(sid);

				_applicationLog.Close();

				//Send Mail
				sendMail();

		

			}
			catch (Exception _ex)
			{
				_applicationLog.WriteLine(DateTime.Now + " |ERROR| " + _ex.Message);
				_applicationLog.Flush();

				_applicationLog.Close();
				_wsClient.logout(sid);
			}
			
			
		}

		//---------------------------------------
		// Custom Methods
		//---------------------------------------
		public static int getSID(string _user, string _pass, StreamWriter _appLog, ITSD_WebServices.USD_WebService _client)
		{
			//-------------------------------
			// Declare Local Variables
			//-------------------------------

			int _sid = 0;

			try
			{   //----------------------
				// Invoke Login Method
				//----------------------
				_sid = _client.login(_user, _pass);
			}
			catch (Exception _exception)
			{
				_appLog.WriteLine(DateTime.Now + " |ERROR| " + _exception.Message);
				_appLog.Flush();


				//Notify Application Support
				_appLog.WriteLine(DateTime.Now + " |INFO| Notify Application Support");
				_appLog.Flush();

				sendSMS("ITSD PROD: Auto Ticket Transfer Process - (getSID) *** Web Service is unavailable ***.  Please investigate.", _appLog);
			}
			
			return _sid;
		}

		public static string getUserHandle(int _sid, string _userid, StreamWriter _appLog, ITSD_WebServices.USD_WebService _client)
		{
			//-------------------------------
			// Declare Local Variables
			//-------------------------------
			string _user = "";
			

			try
			{   //----------------------
				// Invoke Login Method
				//----------------------
				_user = _client.getHandleForUserid(_sid, _userid);
			}
			catch (Exception _exception)
			{
				_appLog.WriteLine(DateTime.Now + " |ERROR| " + _exception.Message);

				_appLog.Flush();

				//Notify Application Support
				_appLog.WriteLine(DateTime.Now + " |INFO| Notify Application Support");
				_appLog.Flush();

				sendSMS("ITSD PROD: Auto Ticket Transfer Process - (getUserHandle) *** Web Service is unavailable ***.  Please investigate.", _appLog);
			}

			return _user;
		}

		public static string getGroupHandle(int sid, string group, ITSD_WebServices.USD_WebService soapClient, StreamWriter appLog)
		{
			//-----------------------------------
			// Variable Declaration
			//-----------------------------------

			string objectType = "cnt";
			string doSelectWhereClause = "last_name = '" + group + "'";
			int maxRows = 1;
			string[] doSelectAttr = new string[1];
			doSelectAttr[0] = "persistent_id";
			string returnGroupHandle = "";

			try
			{
				//-----------------------------------
				// Perform the Web Service Call
				//-----------------------------------

				returnGroupHandle = soapClient.doSelect(sid, objectType, doSelectWhereClause, maxRows, doSelectAttr);
			}
			catch (Exception _error)
			{
				appLog.WriteLine(DateTime.Now + "|ERROR|" + _error.Message);
				appLog.Flush();

				//Notify Application Support
				appLog.WriteLine(DateTime.Now + " |INFO| Notify Application Support");
				appLog.Flush();

				sendSMS("ITSD PROD: Auto Ticket Transfer Process - (getGroupHandle) *** Web Service is unavailable ***.  Please investigate.", appLog);
			}


			return returnGroupHandle;
		}

		public static string getTicketHandle(int sid, string ticketNumber, ITSD_WebServices.USD_WebService soapClient, StreamWriter appLog)
		{
			//-----------------------------------
			// Variable Declaration
			//-----------------------------------

			string objectType = "cr";
			string doSelectWhereClause = "ref_num = '" + ticketNumber + "'";
			int maxRows = 1;
			string[] doSelectAttr = new string[1];
			doSelectAttr[0] = "persistent_id";
			string returnTicketHandle = "";

			try
			{
				//-----------------------------------
				// Perform the Web Service Call
				//-----------------------------------

				returnTicketHandle = soapClient.doSelect(sid, objectType, doSelectWhereClause, maxRows, doSelectAttr);
			}
			catch (Exception _error)
			{
				appLog.WriteLine(DateTime.Now + "|ERROR|" + _error.Message);
				appLog.Flush();

				//Notify Application Support
				appLog.WriteLine(DateTime.Now + " |INFO| Notify Application Support");
				appLog.Flush();

				sendSMS("ITSD PROD: Auto Ticket Transfer Process - (getTicketHandle) *** Web Service is unavailable ***.  Please investigate.", appLog);
			}


			return returnTicketHandle;
		}

		public static string getAssigneeHandle(int sid, string assignee, ITSD_WebServices.USD_WebService soapClient, StreamWriter appLog)
		{
			//-----------------------------------
			// Variable Declaration
			//-----------------------------------

			string objectType = "cnt";
			string doSelectWhereClause = "userid = '" + assignee + "'";
			int maxRows = 1;
			string[] doSelectAttr = new string[1];
			doSelectAttr[0] = "persistent_id";
			string returnAssigneeHandle = "";

			try
			{
				//-----------------------------------
				// Perform the Web Service Call
				//-----------------------------------

				returnAssigneeHandle = soapClient.doSelect(sid, objectType, doSelectWhereClause, maxRows, doSelectAttr);
			}
			catch (Exception _error)
			{
				appLog.WriteLine(DateTime.Now + "|ERROR|" + _error.Message);
				appLog.Flush();

				//Notify Application Support
				appLog.WriteLine(DateTime.Now + " |INFO| Notify Application Support");
				appLog.Flush();

				sendSMS("ITSD PROD: Auto Ticket Transfer Process - (getAssigneeHandle) *** Web Service is unavailable ***.  Please investigate.", appLog);
			}


			return returnAssigneeHandle;
		}

		public static List<string> getViewData(StreamWriter _appLog)
		{
			//---------------------------------
			// Declaring Local Variables
			//---------------------------------
			List<string> _TicketList = new List<string>();
			SqlConnection _connection = new SqlConnection(ConfigurationManager.ConnectionStrings["Neville_View"].ConnectionString);
			SqlCommand _selectCommand = new SqlCommand();

			try
			{
				_connection.Open();

				//---------------------------
				// GET count of stuck order
				// transaction in VIEW
				//---------------------------
				_selectCommand.Connection = _connection;
				_selectCommand.CommandText = ConfigurationManager.AppSettings["selectquery"];
				SqlDataReader _dataReader = _selectCommand.ExecuteReader();



				while (_dataReader.Read())
				{
					_TicketList.Add(_dataReader["ref_num"].ToString() + "|" + _dataReader["new_group"].ToString() + "|" + _dataReader["new_assignee_id"].ToString() + "|" + _dataReader["description"]);

					_appLog.WriteLine(_dataReader["ref_num"].ToString() + "|" + _dataReader["new_group"].ToString() + "|" + _dataReader["new_assignee_id"].ToString());
				}

				//---------------------------
				// Closing the Reader and
				// DB Connection
				//---------------------------

				_connection.Close();
				_appLog.Flush();

			}
			catch (Exception _ex)
			{
				//----------------------------
				// Logging any exception to
				// the Application Log
				//----------------------------
				_appLog.WriteLine(DateTime.Now + "  " + _ex.Message);
				_appLog.WriteLine(DateTime.Now + "  " + _ex.StackTrace);
				_appLog.Flush();

				//Notify Application Support
				_appLog.WriteLine(DateTime.Now + " |INFO| Notify Application Support");
				_appLog.Flush();

				sendSMS("ITSD PROD: Auto Ticket Transfer Process - (getViewData) *** Unable to read data from VIEW ***.  Please contact Neville for assistance.", _appLog);
			}
			finally
			{
				_connection.Close();
				_appLog.Flush();

			}


			return _TicketList;
		}

		public static void sendSMS(string msg, StreamWriter appLog)
		{
			string sURL = ConfigurationManager.AppSettings["SMS_Service"] + msg;

			try
			{
				WebRequest wrGETURL;
				wrGETURL = WebRequest.Create(sURL);

				WebProxy myProxy = new WebProxy("myproxy", 80);
				myProxy.BypassProxyOnLocal = true;

				wrGETURL.Proxy = WebProxy.GetDefaultProxy();

				wrGETURL.GetResponse().GetResponseStream();

			}
			catch (Exception _err)
			{
				appLog.WriteLine(DateTime.Now + "|ERROR|Could not sent SMS");
				appLog.Flush();

				appLog.WriteLine(DateTime.Now + "|ERROR|" + _err.Message);
				appLog.Flush();
			}



		}

		public static void sendMail()
		{
			System.Net.Mail.MailMessage message = new System.Net.Mail.MailMessage();
			message.To.Add(ConfigurationManager.AppSettings["recipients"]);
			message.CC.Add(ConfigurationManager.AppSettings["cclist"]);
			message.Subject = ConfigurationManager.AppSettings["subject"];
			message.From = new System.Net.Mail.MailAddress(ConfigurationManager.AppSettings["From"]);
			message.IsBodyHtml = true;
			message.Body = "<table style='border: 1px solid black; border-collapse:collapse;'>" +
							"<tr>" +
							"<th width='350' style='border: 1px solid black; background-color:yellow; color:black; font-family:verdana;'>Total Number of Tickets Transferred</th >" +
							"</tr>" +
							"<tr>" +
								"<td width='350' style='border: 1px solid black; text-align:center;'>" + _transferCount + "</td>" +
								"</tr>" +
							"</table>";

			System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient(ConfigurationManager.AppSettings["mailserver"]);
			System.Net.Mail.Attachment _attachment1 = new System.Net.Mail.Attachment(ConfigurationManager.AppSettings["ApplicationLog"] + "_" + DateTime.Now.Year.ToString() + DateTime.Now.Month.ToString() + DateTime.Now.Day.ToString() + ".log");
			message.Attachments.Add(_attachment1);
			smtp.Send(message);
		}


	}
}
