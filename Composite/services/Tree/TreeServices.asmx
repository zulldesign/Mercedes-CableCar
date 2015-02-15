<%@ WebService Language="C#" Class="Composite.Services.TreeServices" %>

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web.Services;
using System.Web.Services.Protocols;
using System.Xml.Linq;
using Composite.C1Console.Actions;
using Composite.C1Console.Events;
using Composite.C1Console.Elements;
using Composite.C1Console.Security;
using Composite.C1Console.Users;
using Composite.Core;
using Composite.Core.Extensions;
using Composite.Core.IO;
using Composite.Core.Routing;
using Composite.Core.Xml;
using Composite.Core.Types;
using Composite.Core.WebClient.Services.TreeServiceObjects;
using Composite.Core.WebClient.FlowMediators;
using Composite.Core.WebClient.Services.TreeServiceObjects.ExtensionMethods;
using Composite.Data;
using Composite.Data.ProcessControlled;
using Composite.Data.Types;

// Search token stuff
using Composite.Plugins.Elements.ElementProviders.MediaFileProviderElementProvider;
using Composite.Plugins.Elements.ElementProviders.AllFunctionsElementProvider;

namespace Composite.Services
{
    [WebService(Namespace = "http://www.composite.net/ns/management")]
    [SoapDocumentService(RoutingStyle = SoapServiceRoutingStyle.RequestElement)]
    public class TreeServices : WebService
    {
        private const string LogTitle = "TreeService";
        
        private void RemoveDuplicateActions(List<ClientElement> listToClean)
        {
            List<string> knownActionKeys = new List<string>();
            foreach (ClientElement clientElement in listToClean)
            {
                clientElement.Actions.RemoveAll(f => knownActionKeys.Contains(f.ActionKey));
                knownActionKeys.AddRange(clientElement.ActionKeys.Where(f => !knownActionKeys.Contains(f)));
            }
        }



        [WebMethod]
        public List<ClientElement> GetActivePerspectiveElements(string dummy)
        {
            try
            {
                string username = UserValidationFacade.GetUsername();

                List<Element> allPerspectives = ElementFacade.GetPerspectiveElementsWithNoSecurity().ToList();
                List<string> activePerspectiveEntityTokens = UserPerspectiveFacade.GetSerializedEntityTokens(username).ToList();
                activePerspectiveEntityTokens.AddRange(UserGroupPerspectiveFacade.GetSerializedEntityTokens(username));
                activePerspectiveEntityTokens = activePerspectiveEntityTokens.Distinct().ToList();

                List<ClientElement> activePerspectives = allPerspectives.Where(f => activePerspectiveEntityTokens.Contains(EntityTokenSerializer.Serialize(f.ElementHandle.EntityToken))).ToList().ToClientElementList();

                foreach (ClientElement clientElement in activePerspectives)
                {
                    clientElement.Actions.Clear();
                    clientElement.ActionKeys.Clear();
                }

                return activePerspectives;
            }
            catch (Exception ex)
            {
                Log.LogCritical(LogTitle, "Unable to get any perspectives, console will not work!");
                Log.LogCritical(LogTitle, ex);
                return new List<ClientElement>();
            }
        }



        [WebMethod]
        public List<ClientElement> GetElements(ClientElement clientElement)
        {
            return GetElementsBySearchToken(clientElement, null);
        }


        [WebMethod]
        public List<ClientElement> GetRootElements(string dummy)
        {
            return GetElementsBySearchToken(null, null);
        }


        [WebMethod]
        public List<RefreshChildrenInfo> FindEntityToken(string rootEntityToken, string entityToken, List<RefreshChildrenParams> openedNodes)
        {
            Verify.ArgumentNotNullOrEmpty(rootEntityToken, "rootEntityToken");
            Verify.ArgumentNotNullOrEmpty(entityToken, "entityToken");
            Verify.ArgumentNotNull(openedNodes, "openedNodes");

            List<RefreshChildrenInfo> refreshingInfo = TreeServicesFacade.FindEntityToken(rootEntityToken, entityToken, openedNodes);
            if (refreshingInfo == null)
            {
                return new List<RefreshChildrenInfo>();
            }

            foreach (RefreshChildrenInfo nodeRefreshingInfo in refreshingInfo)
            {
                RemoveDuplicateActions(nodeRefreshingInfo.ClientElements);
            }
            return refreshingInfo;
        }


        [WebMethod]
        public List<RefreshChildrenInfo> GetMultipleChildren(List<RefreshChildrenParams> clientProviderNameEntityTokenPairs)
        {
            Verify.ArgumentNotNull(clientProviderNameEntityTokenPairs, "clientProviderNameEntityTokenPairs");

            List<RefreshChildrenInfo> multiChildren = TreeServicesFacade.GetMultipleChildren(clientProviderNameEntityTokenPairs);
            foreach (RefreshChildrenInfo multiChild in multiChildren)
            {
                RemoveDuplicateActions(multiChild.ClientElements);
            }
            return multiChildren;
        }



        [WebMethod]
        public List<ClientElement> GetElementsBySearchToken(ClientElement clientElement, string serializedSearchToken)
        {
            VerifyClientElement(clientElement);

            if (clientElement == null || string.IsNullOrEmpty(clientElement.ProviderName))
            {
                List<ClientElement> root = new List<ClientElement>();
                root.Add(TreeServicesFacade.GetRoot());
                return root;
            }
            
            List<ClientElement> clientElements = TreeServicesFacade.GetChildren(clientElement.ProviderName, clientElement.EntityToken, clientElement.Piggybag, serializedSearchToken);
            RemoveDuplicateActions(clientElements);
            return clientElements;
            
        }



        [WebMethod]
        public List<ClientElement> GetNamedRoots(string name)
        {
            return GetNamedRootsBySearchToken(name, null);
        }



        [WebMethod]
        public List<ClientElement> GetNamedRootsBySearchToken(string name, string serializedSearchToken)
        {
            Verify.ArgumentNotNullOrEmpty(name, "name");

            List<ClientElement> clientElements = TreeServicesFacade.GetRoots(name, serializedSearchToken);
            RemoveDuplicateActions(clientElements);
            return clientElements;
        }



        [WebMethod]
        public string GetEntityTokenByPageUrl(string pageUrl)
        {
            UrlKind urlKind;
            
            PageUrlData pageUrlData = PageUrls.ParseUrl(pageUrl, out urlKind);
            if (pageUrlData == null) return string.Empty;

            if (pageUrlData.PublicationScope == PublicationScope.Published)
            {
                pageUrlData.PublicationScope = PublicationScope.Unpublished;
            }

            IPage page = pageUrlData.GetPage();
            if (page == null) return string.Empty;

            return EntityTokenSerializer.Serialize(page.GetDataEntityToken(), true);
        }



        [WebMethod]
        public List<ClientLabeledProperty> GetProperties(ClientElement clientElement)
        {
            VerifyClientElement(clientElement);
            return TreeServicesFacade.GetLabeledProperties(clientElement.ProviderName, clientElement.EntityToken, clientElement.Piggybag);
        }              
        


        [WebMethod]
        public List<ActionResult> ExecuteSingleElementAction(ClientElement clientElement, string serializedActionToken, string consoleId)
        {
            try
            {
                VerifyClientElement(clientElement);
                TreeServicesFacade.ExecuteElementAction(clientElement.ProviderName, clientElement.EntityToken, clientElement.Piggybag, serializedActionToken, consoleId);
            }
            catch (Exception ex)
            {
                Log.LogError(LogTitle, ex);

                IConsoleMessageQueueItem errorLogEntry = new LogEntryMessageQueueItem { Sender = typeof(TreeServices), Level = Composite.Core.Logging.LogLevel.Error, Message = ex.ToString() };
                ConsoleMessageQueueFacade.Enqueue(errorLogEntry, consoleId);
                IConsoleMessageQueueItem msgBoxEntry = new MessageBoxMessageQueueItem { DialogType = DialogType.Error, Title = "Error executing action", Message = "An error occured executing the action. Please contact your system administrator or consult the log for help" };
                ConsoleMessageQueueFacade.Enqueue(msgBoxEntry, consoleId);
            }

            return new List<ActionResult> { new ActionResult { ResponseType = ActionResultResponseType.None } };
        }



        [WebMethod]
        public bool ExecuteDropElementAction(ClientElement draggedClientElement, ClientElement newParentClientElement, int dropIndex, string consoleId, bool isCopy)
        {
            try
            {
                VerifyClientElement(draggedClientElement);
                VerifyClientElement(newParentClientElement);
                return TreeServicesFacade.ExecuteElementDraggedAndDropped(draggedClientElement.ProviderName, draggedClientElement.EntityToken, draggedClientElement.Piggybag, newParentClientElement.ProviderName, newParentClientElement.EntityToken, newParentClientElement.Piggybag, dropIndex, consoleId, isCopy);
            }
            catch (Exception ex)
            {
                IConsoleMessageQueueItem errorLogEntry = new LogEntryMessageQueueItem { Sender = typeof(TreeServices), Level = Composite.Core.Logging.LogLevel.Error, Message = ex.Message };
                ConsoleMessageQueueFacade.Enqueue(errorLogEntry, consoleId);

                throw;
            }
        }


        [WebMethod]
        public List<KeyValuePair> GetSearchTokens(string dummy)
        {
            List<KeyValuePair> tokens = new List<KeyValuePair>();


            MediaFileSearchToken embedableMediaFileSearchToken = new MediaFileSearchToken();
            embedableMediaFileSearchToken.MimeTypes = new string[] { MimeTypeInfo.Asf, MimeTypeInfo.Avi, MimeTypeInfo.Director, MimeTypeInfo.Flash, MimeTypeInfo.QuickTime, MimeTypeInfo.Wmv };
            tokens.Add(new KeyValuePair("MediaFileElementProvider.EmbeddableMedia", embedableMediaFileSearchToken.Serialize()));

            MediaFileSearchToken imageMediaFileSearchToken = new MediaFileSearchToken();
            imageMediaFileSearchToken.MimeTypes = new string[] { MimeTypeInfo.Gif, MimeTypeInfo.Jpeg, MimeTypeInfo.Png, MimeTypeInfo.Bmp, MimeTypeInfo.Svg };
            tokens.Add(new KeyValuePair("MediaFileElementProvider.WebImages", imageMediaFileSearchToken.Serialize()));

            MediaFileSearchToken writableMediaFolderSearchToken = new MediaFileSearchToken();
            writableMediaFolderSearchToken.MimeTypes = new string[] { "." };
            tokens.Add(new KeyValuePair("MediaFileElementProvider.WritableFolders", writableMediaFolderSearchToken.Serialize()));

            var xhtmlDocumentFunctionsSearchToken = AllFunctionsElementProviderSearchToken.Build(new[] { typeof(XhtmlDocument), typeof(System.Web.UI.Control) });
            tokens.Add(new KeyValuePair("AllFunctionsElementProvider.VisualEditorFunctions", xhtmlDocumentFunctionsSearchToken.Serialize()));

            var xstlFunctionCallsSearchToken = AllFunctionsElementProviderSearchToken.Build(new[] { typeof(XhtmlDocument), typeof(IEnumerable<XElement>), typeof(XElement) });
            tokens.Add(new KeyValuePair("AllFunctionsElementProvider.XsltFunctionCall", xstlFunctionCallsSearchToken.Serialize()));

            return tokens;
        }

        [WebMethod]
        public bool ExecuteInlineElementAction(string serializedScriptAction, string consoleId)
        {
            InlineScriptActionFacade.ExecuteElementScriptAction(serializedScriptAction, consoleId);

            return true;
        }
        


        private void VerifyClientElement(ClientElement clientElement)
        {
            if (clientElement == null) return;

            if (!HashSigner.ValidateSignedHash(clientElement.Piggybag, HashValue.Deserialize(clientElement.PiggybagHash)))
            {
                throw new System.Security.SecurityException("Data has been tampered");
            }
        }

		[WebMethod]
		public List<KeyValuePair> GetDefaultEntityTokens(string dummy)
		{
			List<KeyValuePair> tokens = new List<KeyValuePair>();
			using (var connection = new DataConnection())
			{
				var homepage = PageServices.GetChildren(Guid.Empty).FirstOrDefault();
				if (homepage != null)
				{
					tokens.Add(
						new KeyValuePair(
							EntityTokenSerializer.Serialize(AttachingPoint.ContentPerspective.EntityToken, true),
							EntityTokenSerializer.Serialize(homepage.GetDataEntityToken(), true)
							)
						);
				}
				tokens.Add(
					new KeyValuePair(
						EntityTokenSerializer.Serialize(AttachingPoint.SystemPerspective.EntityToken, true),
						EntityTokenSerializer.Serialize(new Composite.Plugins.Elements.ElementProviders.PackageElementProvider.PackageElementProviderAvailablePackagesFolderEntityToken(), true)
						)
					);
					
				
			}
			return tokens;
		}

		[WebMethod]
		public List<string> GetCurrentLocaleEntityTokens(List<string> serializedEntityTokens)
		{
			var currentLocaleEntityTokens = new List<string>();
			foreach (var serializedEntityToken in serializedEntityTokens)
			{
				try
				{
					var entityToken = EntityTokenSerializer.Deserialize(serializedEntityToken);
					if (entityToken is DataEntityToken)
					{
						var dataItem = (entityToken as DataEntityToken).Data;
						if (dataItem is ILocalizedControlled)
						{
							var dataItemFromTheotherLocale = DataFacade.GetDataFromOtherLocale(dataItem, UserSettings.ActiveLocaleCultureInfo).ToList();

                            if (!dataItemFromTheotherLocale.Any() && UserSettings.ForeignLocaleCultureInfo != null)
						    {
                                dataItemFromTheotherLocale = DataFacade.GetDataFromOtherLocale(dataItem, UserSettings.ForeignLocaleCultureInfo).ToList();
						    }
                            
							if (dataItemFromTheotherLocale.Count == 1)
							{
							    var foreignEntityToken = dataItemFromTheotherLocale[0].GetDataEntityToken();

                                currentLocaleEntityTokens.Add(EntityTokenSerializer.Serialize(foreignEntityToken, true));
								continue;
							}
						}
					}
					currentLocaleEntityTokens.Add(serializedEntityToken);
				}
				catch
				{
				}
			}
			return currentLocaleEntityTokens;
		}

		[WebMethod]
		public List<string> GetAllParents(string serializedEntityToken)
		{
			var entityToken = EntityTokenSerializer.Deserialize(serializedEntityToken);
			var graph = new RelationshipGraph(entityToken, RelationshipGraphSearchOption.Both, true);
			var tokens = new HashSet<EntityToken>();

			foreach (var level in graph.Levels)
			{
				tokens.UnionWith(level.AllEntities);
			}

			return tokens.Select(d => EntityTokenSerializer.Serialize(d,true)).ToList();
		}
		
		
		[WebMethod]
		public string GetCompositeUrlLabel(string path)
		{
			var relativePath = Regex.Replace(path, @"^http://[\w\.\d:]+/", "/");
			var mediaUrlData = MediaUrls.ParseUrl(relativePath);

			using (var conn = new DataConnection())
			{
				if (mediaUrlData != null)
				{
					var mediaId = mediaUrlData.MediaId;
					var store = mediaUrlData.MediaStore;

					var matchingMedia = conn.Get<IMediaFile>().FirstOrDefault(media => media.Id == mediaId && media.StoreId == store);

					if (matchingMedia != null)
					{
						string label = string.Format("{0} ({1}:{2})", matchingMedia.FileName, matchingMedia.StoreId, matchingMedia.FolderPath);
						return label;
					}
				}

				var pageUrlData = PageUrls.ParseUrl(relativePath);
				if (pageUrlData != null)
				{
					var pageNode = conn.SitemapNavigator.GetPageNodeById(pageUrlData.PageId);

					if (pageNode != null)
					{
						string label = string.Format("{0} ({1})", pageNode.Title, pageNode.Url);
						return label;
					}
				}
			}
			
			return path;
		}

		[WebMethod]
		public string GetCompositeEntityToken(string path)
		{
			var relativePath = Regex.Replace(path, @"^http://[\w\.\d:]+/", "/");
			var mediaUrlData = MediaUrls.ParseUrl(relativePath);

			using (var connection = new DataConnection())
			{
				if (mediaUrlData != null)
				{
					var mediaId = mediaUrlData.MediaId;
					var store = mediaUrlData.MediaStore;

					var matchingMedia = connection.Get<IMediaFile>().FirstOrDefault(media => media.Id == mediaId && media.StoreId == store);

					if (matchingMedia != null)
					{
						return EntityTokenSerializer.Serialize(matchingMedia.GetDataEntityToken(), true);
					}
				}

				var pageUrlData = PageUrls.ParseUrl(relativePath);
				if (pageUrlData != null)
				{
					var page = PageManager.GetPageById(pageUrlData.PageId);

					if (page != null)
					{
						return EntityTokenSerializer.Serialize(page.GetDataEntityToken(), true);
					}
				}
			}

			return null;
		}
	}
}