using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Xml;
using System.Text.Json;

namespace WcfService1
{
    // NOTE: You can use the "Rename" command on the "Refactor" menu to change the class name "Service1" in code, svc and config file together.
    // NOTE: In order to launch WCF Test Client for testing this service, please select Service1.svc or Service1.svc.cs at the Solution Explorer and start debugging.
    public class Service1 : IService1
    {
        public string GetData(int value)
        {
            return string.Format("You entered: {0}", value);
        }

        public CompositeType GetDataUsingDataContract(CompositeType composite)
        {
            if (composite == null)
            {
                throw new ArgumentNullException("composite");
            }
            if (composite.BoolValue)
            {
                composite.StringValue += "Suffix";
            }
            return composite;
        }

        private string ConvertDictionaryToJson(Dictionary<string, object> dictionary)
        {
            return JsonSerializer.Serialize(dictionary);
        }

        private bool loggedIn(Session currentSession)
        {
            XmlHelper xmlHelper = new XmlHelper();
            XmlDocument xmlDoc = xmlHelper.LoadXml(currentSession.UserType);
            XmlNode userNode = xmlDoc.SelectSingleNode($"/Users/User[UserName='{currentSession.UserName}']");
            if ((userNode !=null) && (userNode["SessionId"].InnerText == currentSession.SessionId))
            {
                var lastLogin = DateTime.Parse(userNode["LastLogin"].InnerText);
                if ((DateTime.Now - lastLogin).TotalDays < 2)
                {
                    return true;
                }
            }
            return false;
        }
        public string Login(string userName, string password, string userType = "Member")
        {
            var result = new Dictionary<string, object>();
            try
            {
                XmlHelper xmlHelper = new XmlHelper();
                XmlDocument xmlDoc = xmlHelper.LoadXml(userType);

                XmlNode userNode = xmlDoc.SelectSingleNode($"/Users/User[UserName='{userName}']");
                if (userNode == null)
                {
                    result["status"] = false;
                    result["message"] = "No User Found";
                }
                else if (userNode["Password"].InnerText == password)
                {
                    userNode["LastLogin"].InnerText = DateTime.Now.ToString();
                    userNode["SessionId"].InnerText = Guid.NewGuid().ToString();
                    xmlHelper.SaveXml(xmlDoc, userType);
                    result["status"] = true;
                    result["message"] = "ignore";
                    result["currentSession"] = new Session
                    {
                        UserName = userName,
                        SessionId = userNode["SessionId"].InnerText,
                        UserType = userType
                    };
                }
                else
                {
                    result["status"] = false;
                    result["message"] = "Incorrect password";
                }
                
            }
            catch (Exception ex)
            {
                result["status"] = false;
                result["message"] = "exception encountered";
                result["exception"] = ex.Message;
            }
            return ConvertDictionaryToJson(result);
        }

        public string SignUp(string userName, string password, string userType = "Member", Session currentSession = null)
        {
            //var output = new Dictionary<string, object>();
            //return output;
            var result = new Dictionary<string, object>();
            try
            {
                if (userType == "Staff")
                {
                    if (!loggedIn(currentSession))
                    {
                        result["status"] = false;
                        result["message"] = "Unauthorized";
                        return ConvertDictionaryToJson(result);
                    }
                }
                XmlHelper xmlHelper = new XmlHelper();
                XmlDocument xmlDoc = xmlHelper.LoadXml(userType);
                XmlNode userNode = xmlDoc.SelectSingleNode($"/Users/User[UserName='{userName}']");
                if (userNode != null) {
                    result["status"] = false;
                    result["message"] = "error: user already exists";
                    return ConvertDictionaryToJson(result);
                }

                // Add new user
                XmlNode newUser = xmlDoc.CreateElement("User");
                XmlNode usernameNode = xmlDoc.CreateElement("UserName");
                usernameNode.InnerText = userName;
                XmlNode passwordNode = xmlDoc.CreateElement("Password");
                passwordNode.InnerText = password;
                XmlNode sessionIdNode = xmlDoc.CreateElement("SessionId");
                sessionIdNode.InnerText = Guid.NewGuid().ToString();
                XmlNode loggedInTimeNode = xmlDoc.CreateElement("LastLogin");
                loggedInTimeNode.InnerText = DateTime.Now.ToString();

                newUser.AppendChild(usernameNode);
                newUser.AppendChild(passwordNode);
                newUser.AppendChild(sessionIdNode);
                newUser.AppendChild(loggedInTimeNode);
                xmlDoc.DocumentElement.AppendChild(newUser);
                xmlHelper.SaveXml(xmlDoc, userType);

                result["status"] = true;
                result["message"] = "User sucessfully created and loggedIn";
                result["currentSession"] = new Session
                {
                    UserName = userName,
                    SessionId = userNode["SessionId"].InnerText,
                    UserType = userType
                };
                if (userType == "Staff")
                {
                    result["currentSession"] = currentSession;
                }
            }
            catch (Exception ex)
            {
                result["status"] = false;
                result["message"] = "exception encountered";
                result["exception"] = ex.Message;
            }

            return ConvertDictionaryToJson(result);
        }

        public string DeleteUser(string userName, string userType, Session currentSession)
        {
            var result = new Dictionary<string, object>();
            try
            {
                if ((currentSession.UserType == "Staff") && loggedIn(currentSession))
                {
                    XmlHelper xmlHelper = new XmlHelper();
                    XmlDocument xmlDoc = xmlHelper.LoadXml(userType);
                    XmlNode userNode = xmlDoc.SelectSingleNode($"/Users/User[UserName='{userName}']");
                    if (userNode == null)
                    {
                        result["status"] = true;
                        result["message"] = "User Not Found!!";
                    }
                    else
                    {
                        XmlNode parentNode = userNode.ParentNode;
                        if (parentNode != null)
                        {
                            parentNode.RemoveChild(userNode);
                            xmlHelper.SaveXml(xmlDoc, userType);
                        }
                        result["status"] = true;
                        result["message"] = "User deleted Sucessfully!!!";
                    }
                    
                }
            }
            catch (Exception ex)
            {
                result["status"] = false;
                result["exception"] = ex.Message;
            }
            result["currentSession"] = currentSession;
            return ConvertDictionaryToJson(result);
        }
    }

    public class XmlHelper
    {
        //private readonly string filePath = "member.xml";

        public XmlDocument LoadXml(string userType)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(UsersXmlPath(userType));
            return xmlDoc;
        }

        public void SaveXml(XmlDocument xmlDoc, string userType)
        {
            xmlDoc.Save(UsersXmlPath(userType));
        }

        private string UsersXmlPath(string userType)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDirectory, $"App_Data/{userType}.xml");
            
        }
    }
}
