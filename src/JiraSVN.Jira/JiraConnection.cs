#region Copyright 2010 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using CSharpTest.Net.Serialization;
using CSharpTest.Net.Serialization.StorageClasses;
using JiraSVN.Common.Interfaces;
using JiraSVN.Jira.Jira;

namespace JiraSVN.Jira
{
	internal class JiraConnection : IIssuesServiceConnection
	{
		private const string ALL_USERS_KEY = "__ALL_USERS__";
		private readonly INameValueStore _store = new RegistryStorage();
		private readonly string _userName, _password, _rootUrl;
		private readonly bool _lookupUsers;
		private readonly JiraUser _currentUser;
		private readonly Dictionary<string, JiraUser> _knownUsers;
		private readonly Dictionary<string, JiraStatus> _statuses;
		private readonly Converter<string, string> _settings;

		private readonly JiraSoapServiceService _service;
		private string _token;

		public JiraConnection(string url, string userName, string password, Converter<string, string> settings)
		{
			Log.Verbose("Creating a new connection");
			_rootUrl = url.TrimEnd('\\', '/');
			int offset = _rootUrl.LastIndexOf("/rpc/soap/jirasoapservice-v2");
			if (offset > 0)
				_rootUrl = _rootUrl.Substring(0, offset);

			Log.Info("Root Url: {0}", _rootUrl);
			_settings = settings;
			_knownUsers = new Dictionary<string, JiraUser>(StringComparer.OrdinalIgnoreCase);
			_lookupUsers = StringComparer.OrdinalIgnoreCase.Equals(settings("resolveUserNames"), "true");
			Log.Info("LookUp Users: {0}", _lookupUsers);

			LoadUsers();
			_statuses = new Dictionary<string, JiraStatus>(StringComparer.Ordinal);

			_userName = userName;
			_password = password;
			Log.Verbose("Creating a new internal Soap Service");
			_service = new JiraSoapServiceService();
			Log.Verbose("Service Created");
			_service.Url = _rootUrl + "/rpc/soap/jirasoapservice-v2";
			_service.UseDefaultCredentials = true;
			if (!String.IsNullOrEmpty(settings("jira:proxyurl")))
			{
				ICredentials proxyAuth = CredentialCache.DefaultCredentials;
				var proxyuri = new UriBuilder(settings("jira:proxyurl"));
				if (!String.IsNullOrEmpty(proxyuri.UserName))
				{
					proxyAuth = new NetworkCredential(proxyuri.UserName, proxyuri.Password);
					proxyuri.Password = proxyuri.UserName = String.Empty;
				}
				_service.Proxy = new WebProxy(proxyuri.Uri);
				_service.Proxy.Credentials = proxyAuth;
			}

			_token = null;

			Log.Verbose("Connecting...");
			Connect();
			Log.Verbose("Connection Successfull");
			_currentUser = GetUser(_userName);

			Log.Verbose("Getting Statuses");
			foreach (RemoteStatus rs in _service.getStatuses(_token))
				_statuses[rs.id] = new JiraStatus(rs);

			Log.Verbose("Finished creating a new connection");
		}

		private void LoadUsers()
		{
			string usersList;
			if (_store.Read(GetType().FullName, ALL_USERS_KEY, out usersList))
			{
				foreach (string suser in usersList.Split(';'))
				{
					if (_lookupUsers)
					{
						string fullName;
						if (_store.Read(GetType().FullName, suser, out fullName))
							_knownUsers[suser] = new JiraUser(suser, fullName);
					}
					else
						_knownUsers[suser] = new JiraUser(suser, suser);
				}
			}
		}

		public void Dispose()
		{
			try
			{
				if (!String.IsNullOrEmpty(_token)) _service.logout(_token);
			}
			catch
			{
			}
			finally
			{
				_token = null;
			}

			_service.Dispose();
		}

		protected string Token
		{
			get { return _token; }
		}

		protected JiraSoapServiceService Service
		{
			get { return _service; }
		}

		private void Connect()
		{
			try
			{
				if (!String.IsNullOrEmpty(_token)) _service.logout(_token);
			}
			catch
			{
			}
			finally
			{
				_token = null;
			}

			_token = _service.login(_userName, _password);

			if (String.IsNullOrEmpty(_token))
				throw new ApplicationException("Access denied.");
		}

		public IIssueUser CurrentUser
		{
			get { return _currentUser; }
		}

		public IIssueFilter[] GetFilters()
		{
			var filters = new List<IIssueFilter>();

			try
			{
				foreach (RemoteFilter filter in _service.getSavedFilters(Token))
					filters.Add(new JiraFilter(this, filter));
			}
			catch (Exception e)
			{
				Log.Warning(e);
			}
			try
			{
				foreach (RemoteFilter filter in _service.getFavouriteFilters(Token))
				{
					var jfilt = new JiraFilter(this, filter);
					if (!filters.Contains(jfilt))
						filters.Add(jfilt);
				}
			}
			catch (Exception e)
			{
				Log.Warning(e);
			}

			filters.Sort(new NameSorter<IIssueFilter>());

			//RemoteServerInfo jiraInfo = _service.getServerInfo(_token);
			//if (new Version(jiraInfo.version) < new Version("4.0"))
			filters.Add(new JiraAllFilter(this));

			return filters.ToArray();
		}

		public IIssueUser[] GetUsers()
		{
			return new List<JiraUser>(_knownUsers.Values).ToArray();
		}

		internal JiraUser GetUser(string name)
		{
			JiraUser user;
			if (String.IsNullOrEmpty(name))
				return JiraUser.Unknown;

			if (!_knownUsers.TryGetValue(name, out user))
			{
				if (_lookupUsers)
					_knownUsers.Add(name, user = new JiraUser(_service.getUser(_token, name)));
				else
					_knownUsers.Add(name, user = new JiraUser(name, name));

				_store.Write(GetType().FullName, user.Id, user.Name);
				_store.Write(GetType().FullName, ALL_USERS_KEY,
				             String.Join(";", new List<String>(_knownUsers.Keys).ToArray()));
			}

			return user;
		}

		internal JiraStatus GetStatus(string status)
		{
			JiraStatus js;
			if (status != null && _statuses.TryGetValue(status, out js))
				return js;
			return JiraStatus.Unknown;
		}

		internal void ViewIssue(JiraIssue issue)
		{
			string url = String.Format("{0}/browse/{1}", _rootUrl, issue.DisplayId);

			try
			{
				Process.Start(url);
			}
			catch (Exception e)
			{
				Log.Error(e, "Failed to view issue at uri = {0}.", url);
			}
		}

		internal IIssue[] GetIssuesByFilter(JiraFilter filter, int offsett, int maxNumber)
		{
			var issues = new List<JiraIssue>();
			foreach (RemoteIssue issue in _service.getIssuesFromFilterWithLimit(_token, filter.Id, offsett, maxNumber))
				issues.Add(new JiraIssue(this, issue));
			return issues.ToArray();
		}

		internal IIssue[] GetAllIssues(string text, int offsett, int maxNumber)
		{
			var issues = new List<JiraIssue>();
			//RemoteProject[] projects = _service.getProjectsNoSchemes(_token);
			//RemoteIssue[] allissues = _service.getIssuesFromTextSearchWithProject(_token, new string[] { projects[0].key }, " ", 100);
			RemoteIssue[] allissues = _service.getIssuesFromTextSearchWithLimit(_token, text, offsett, maxNumber);
			foreach (RemoteIssue issue in allissues)
				issues.Add(new JiraIssue(this, issue));
			return issues.ToArray();
		}

		internal void AddComment(JiraIssue issue, string comment)
		{
			if (String.IsNullOrEmpty(comment)) return;

			var rc = new RemoteComment();
			rc.author = CurrentUser.Id;
			rc.body = comment;
			rc.created = DateTime.Now;

			_service.addComment(_token, issue.DisplayId, rc);
		}

		internal JiraAction[] GetActions(JiraIssue issue)
		{
			var results = new List<JiraAction>();
			try
			{
				RemoteNamedObject[] actionsAvailable = _service.getAvailableActions(_token, issue.DisplayId);
				if (actionsAvailable != null) //dumbasses
					foreach (RemoteNamedObject item in actionsAvailable)
						results.Add(new JiraAction(item));
			}
			catch
			{
			}
			return results.ToArray();
		}

		private string FindFixResolution()
		{
			foreach (RemoteResolution res in _service.getResolutions(_token))
			{
				if (res.name.IndexOf("fix", StringComparison.OrdinalIgnoreCase) >= 0)
					return res.id;
			}
			throw new ApplicationException("Unable to locate a resolution containing the text 'fix'.");
		}

		internal void ProcessAction(JiraIssue issue, IIssueAction action, IIssueUser assignTo)
		{
			var actionParams = new List<RemoteFieldValue>();

			RemoteField[] fields = _service.getFieldsForAction(_token, issue.DisplayId, action.Id);
			foreach (RemoteField field in fields)
			{
				var param = new RemoteFieldValue();
				string paramName = param.id = field.id;

				if (StringComparer.OrdinalIgnoreCase.Equals("Resolution", field.name))
					param.values = new[] {FindFixResolution()};
				else if (StringComparer.OrdinalIgnoreCase.Equals("Assignee", field.name))
					param.values = new[] {assignTo.Id};
				else if (StringComparer.OrdinalIgnoreCase.Equals("Worklog", paramName)) // JIRA 4.1 - worklogs are required!
					continue;
				else
				{
					param.values = issue.GetFieldValue(paramName);
					if (param.values == null || param.values.Length == 0 || (param.values.Length == 1 && param.values[0] == null))
					{
						string setting = _settings(String.Format("{0}:{1}", action.Name, field.name));
						if (setting != null)
							param.values = new[] {setting};
					}
				}

				actionParams.Add(param);
			}

			RemoteIssue newIssue = _service.progressWorkflowAction(_token, issue.DisplayId, action.Id, actionParams.ToArray());
		}

		internal void ProcessWorklog(JiraIssue issue, string timeSpent, TimeEstimateRecalcualationMethod method,
		                             string newTimeEstimate)
		{
			var remoteWorklog = new RemoteWorklog();
			remoteWorklog.comment = "Time logged";
			remoteWorklog.timeSpent = timeSpent;
			remoteWorklog.startDate = DateTime.Now;

			switch (method)
			{
				case TimeEstimateRecalcualationMethod.AdjustAutomatically:
					_service.addWorklogAndAutoAdjustRemainingEstimate(_token, issue.DisplayId, remoteWorklog);
					break;

				case TimeEstimateRecalcualationMethod.DoNotChange:
					_service.addWorklogAndRetainRemainingEstimate(_token, issue.DisplayId, remoteWorklog);
					break;
				case TimeEstimateRecalcualationMethod.SetToNewValue:
					_service.addWorklogWithNewRemainingEstimate(_token, issue.DisplayId, remoteWorklog,
					                                            newTimeEstimate);
					break;
				default:
					throw new ArgumentOutOfRangeException("ProcessWorklog");
			}
		}
	}
}