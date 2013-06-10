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
using JiraSVN.Common.Interfaces;

namespace JiraSVN.Plugin.UI
{
	internal class IssueItemView : IIssue
	{
		private readonly IssuesListView _view;
		private readonly IIssue _issue;

		public IssueItemView(IssuesListView view, IIssue issue)
		{
			_view = view;
			_issue = issue;
		}

		public bool Selected
		{
			get { return _view.IsSelected(this); }
			set { _view.Select(this, value); }
		}

		public string Id
		{
			get { return _issue.Id; }
		}

		public string Name
		{
			get { return _issue.Name; }
		}

		public string DisplayId
		{
			get { return _issue.DisplayId; }
		}

		public string FullDescription
		{
			get { return _issue.FullDescription; }
		}

		public IIssueState CurrentState
		{
			get { return _issue.CurrentState; }
		}

		public IIssueUser AssignedTo
		{
			get { return _issue.AssignedTo; }
		}

		public IIssueUser ReportedBy
		{
			get { return _issue.ReportedBy; }
		}

		public DateTime CreatedOn
		{
			get { return _issue.CreatedOn; }
		}

		public DateTime LastModifiedOn
		{
			get { return _issue.LastModifiedOn; }
		}

		public void View()
		{
			_issue.View();
		}

		public void AddComment(string comment)
		{
			_issue.AddComment(comment);
		}

		public IIssueAction[] GetActions()
		{
			return _issue.GetActions();
		}

		public void ProcessAction(string comment, IIssueAction action, IIssueUser assignTo)
		{
			_issue.ProcessAction(comment, action, assignTo);
		}

		public void ProcessWorklog(string timeSpent, TimeEstimateRecalcualationMethod method, string newTimeEstimate)
		{
			_issue.ProcessWorklog(timeSpent, method, newTimeEstimate);
		}
	}
}