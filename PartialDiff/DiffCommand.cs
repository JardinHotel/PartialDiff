using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.IO;
using Task = System.Threading.Tasks.Task;
using EnvDTE;

namespace PartialDiff
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class DiffCommand
    {
        /// <summary>
        /// Select Command ID.
        /// </summary>
        public const int SelectCommandID = 0x0100;

        /// <summary>
        /// Diff Command ID.
        /// </summary>
        public const int DiffCommandID = 0x0102;

        /// <summary>
        /// Diff with Clipboard Command ID.
        /// </summary>
        public const int DiffWithClipboardCommandID = 0x0104;

        /// <summary>
        /// Tools.DiffFiles Command Name
        /// </summary>
        public const string DiffCommandName = "Tools.DiffFiles";

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("d7a71c37-983c-4101-ac9f-a8274401c8b7");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private DTE dTE;

        private string selectedText;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiffCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private DiffCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, SelectCommandID);
            var menuItem = new MenuCommand(this.Select, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(CommandSet, DiffCommandID);
            menuItem = new MenuCommand(this.DiffWithPreviousSelection, menuCommandID);
            commandService.AddCommand(menuItem);

            menuCommandID = new CommandID(CommandSet, DiffWithClipboardCommandID);
            menuItem = new MenuCommand(this.DiffWithClipboard, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static DiffCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in DiffCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new DiffCommand(package, commandService);
            Instance.dTE = await package.GetServiceAsync(typeof(DTE)) as DTE;
        }

        private void Select(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Instance.selectedText = (Instance.dTE.ActiveDocument?.Object() as TextDocument)?.Selection.Text ?? string.Empty;
        }

        private void DiffWithPreviousSelection(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            this.Diff(Instance.selectedText);
        }

        private void DiffWithClipboard(object sender, EventArgs e)
        {
            var data = System.Windows.Forms.Clipboard.GetDataObject();
            if (!data.GetDataPresent(System.Windows.Forms.DataFormats.Text))
            {
                return;
            }

            this.Diff((string)data.GetData(System.Windows.Forms.DataFormats.Text));
        }

        private void Diff(string former)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string latter = (Instance.dTE.ActiveDocument?.Object() as TextDocument)?.Selection.Text;
            if (string.IsNullOrWhiteSpace(former) ||
                string.IsNullOrWhiteSpace(latter))
            {
                return;
            }

            string file1 = Path.GetTempFileName();
            string file2 = Path.GetTempFileName();
            using (var sw1 = new StreamWriter(file1))
            using (var sw2 = new StreamWriter(file2))
            {
                sw1.Write(former);
                sw2.Write((Instance.dTE.ActiveDocument?.Object() as TextDocument)?.Selection.Text);
            }

            Instance.dTE.ExecuteCommand(DiffCommandName, $"\"{file1}\" \"{file2}\" \"{Resources.Resources.Former}\" \"{Resources.Resources.Latter}\"");
        }
    }
}
