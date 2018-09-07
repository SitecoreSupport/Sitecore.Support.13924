namespace Sitecore.Support.Shell.Framework.Commands.Media
{
  using global::System;
  using global::System.Collections.Specialized;
  using global::System.Linq;
  using Sitecore.Configuration;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Globalization;
  using Sitecore.Shell.Framework.Commands;
  using Sitecore.Text;
  using Sitecore.Web;
  using Sitecore.Web.UI.Sheer;
  using Sitecore.Web.UI.XamlSharp;

  /// <summary>
  /// Represents the Upload command.
  /// </summary>
  [Serializable]
  public class Upload : Command
  {
    #region Public methods

    /// <summary>
    /// Executes the command in the specified context.
    /// </summary>
    /// <param name="context">
    /// The context.
    /// </param>
    public override void Execute([NotNull] CommandContext context)
    {
      Assert.ArgumentNotNull(context, "context");

      if (context.Items.Length != 1)
      {
        return;
      }

      Item item = context.Items[0];

      var parameters = new NameValueCollection();

      parameters["id"] = StringUtil.GetString(context.Parameters["id"], item.ID.ToString());
      parameters["language"] = item.Language.ToString();
      parameters["version"] = item.Version.ToString();
      parameters["load"] = StringUtil.GetString(context.Parameters["load"]);
      parameters["edit"] = StringUtil.GetString(context.Parameters["edit"]);
      parameters["tofolder"] = StringUtil.GetString(context.Parameters["tofolder"]);
      parameters[State.Client.UsesBrowserWindowsQueryParameterName] =
        StringUtil.GetString(context.Parameters[State.Client.UsesBrowserWindowsQueryParameterName], "0");

      Context.ClientPage.Start(this, "Run", parameters);
    }

    /// <summary>
    /// Queries the state of the command.
    /// </summary>
    /// <param name="context">
    /// The context.
    /// </param>
    /// <returns>
    /// The state of the command.
    /// </returns>
    public override CommandState QueryState([NotNull] CommandContext context)
    {
      Assert.ArgumentNotNull(context, "context");

      if (UIUtil.UseFlashUpload())
      {
        return CommandState.Hidden;
      }

      if (context.Items.Length != 1)
      {
        return CommandState.Hidden;
      }

      Item item = context.Items[0];

      if (!item.Access.CanCreate() || !item.Access.CanRead() || !item.Access.CanWrite() || !item.Access.CanWriteLanguage())
      {
        return CommandState.Disabled;
      }

      return base.QueryState(context);
    }

    #endregion

    #region Protected methods

    /// <summary>
    /// Runs the pipeline.
    /// </summary>
    /// <param name="args">
    /// The arguments.
    /// </param>
    protected void Run([NotNull] ClientPipelineArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      string id = args.Parameters["id"];
      string language = args.Parameters["language"];
      string version = args.Parameters["version"];
      string tofolder = args.Parameters["tofolder"];

      Item item = Client.ContentDatabase.Items[id, Language.Parse(language), Sitecore.Data.Version.Parse(version)];
      if (item == null)
      {
        SheerResponse.Alert("Item not found.");
        return;
      }

      if (tofolder == "1")
      {
        Item parent = item;

        while (parent != null && parent.TemplateID != TemplateIDs.Folder && parent.TemplateID != TemplateIDs.Node && parent.TemplateID != TemplateIDs.MediaFolder && parent.ID != ItemIDs.MediaLibraryRoot &&
          (parent.Template != null && parent.Template.BaseTemplates.All(t => t.ID != TemplateIDs.MediaFolder)))
        {
          parent = parent.Parent;
        }

        if (parent != null)
        {
          item = parent;
        }
      }

      if (args.IsPostBack)
      {
        if (args.HasResult)
        {
          if (args.Parameters["load"] == "1")
          {
            Context.ClientPage.SendMessage(this, "item:load(id=" + args.Result + ")");

            if (NeedToOpenEditDialogAfterUploadClosed() && args.Parameters["edit"] == "1")
            {
              var url = new UrlString("/sitecore/shell/Applications/Content Manager/default.aspx");

              url.Add("sc_content", WebUtil.GetQueryString("sc_content"));
              url["fo"] = args.Result;
              url["mo"] = "popup";
              url["wb"] = "0";
              url["pager"] = "0";
              // Add language for which to display the uploaded item
              url.Append("la", args.Parameters["language"]);
              url[State.Client.UsesBrowserWindowsQueryParameterName] = WebUtil.GetQueryString(State.Client.UsesBrowserWindowsQueryParameterName, StringUtil.GetString(args.Parameters[State.Client.UsesBrowserWindowsQueryParameterName], "0"));

              SheerResponse.ShowModalDialog(url.ToString(), string.Equals(Context.Language.Name, "ja-JP", StringComparison.InvariantCultureIgnoreCase) ? "1115" : "955", "560");
            }
          }
          else
          {
            Context.ClientPage.SendMessage(this, "media:refresh");
          }
        }
      }
      else
      {
        if (UIUtil.UseFlashUpload())
        {
          var url = new UrlString("/sitecore/shell/~/xaml/Sitecore.Shell.Applications.FlashUpload.Simple.aspx");
          url.Add("uri", item.Uri.ToString());
          string database = string.IsNullOrEmpty(WebUtil.GetQueryString("sc_content")) ? item.Database.Name : WebUtil.GetQueryString("sc_content");
          url.Add("sc_content", database);

          if (!NeedToOpenEditDialogAfterUploadClosed())
          {
            url.Add("edit", args.Parameters["edit"]);
          }

          url.Add(State.Client.UsesBrowserWindowsQueryParameterName, args.Parameters[State.Client.UsesBrowserWindowsQueryParameterName]);
          SheerResponse.ShowModalDialog(url.ToString(), "450", "150", string.Empty, true);
        }
        else
        {
          var url = new UrlString("/sitecore/shell/Applications/Media/Upload Media/UploadMedia.aspx");
          item.Uri.AddToUrlString(url);
          url.Append("edit", args.Parameters["edit"]);
          url.Add("sc_content", WebUtil.GetQueryString("sc_content"));
          //url.Add(State.Client.UsesBrowserWindowsQueryParameterName, args.Parameters[State.Client.UsesBrowserWindowsQueryParameterName]);
          SheerResponse.ShowModalDialog(url.ToString(), "450", "200", string.Empty, true);
        }

        args.WaitForPostBack();
      }
    }

    #endregion

    private static bool NeedToOpenEditDialogAfterUploadClosed()
    {
      if (!UIUtil.UseFlashUpload())
      {
        return false;
      }

      if (UIUtil.IsFirefox() && UIUtil.GetBrowserMajorVersion() < 5)
      {
        return true;
      }

      if (UIUtil.IsWebkit()) //BUG: #359313
      {
        return true;
      }

      return false;
    }
  }
}