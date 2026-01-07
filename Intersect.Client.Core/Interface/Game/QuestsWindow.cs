using Intersect.Client.Core;
using Intersect.Client.Framework.File_Management;
using Intersect.Client.Framework.Gwen.Control;
using Intersect.Client.Framework.Gwen.Control.EventArguments;
using Intersect.Client.General;
using Intersect.Client.Interface.Shared;
using Intersect.Client.Localization;
using Intersect.Client.Networking;
using Intersect.Enums;
using Intersect.Framework.Core.GameObjects.Items;
using Intersect.Framework.Core.GameObjects.NPCs;
using Intersect.Framework.Core.GameObjects.Quests;
using Intersect.GameObjects;
using Intersect.Utilities;

namespace Intersect.Client.Interface.Game;


public partial class QuestsWindow
{

    private readonly Button mBackButton;

    private readonly ScrollControl mQuestDescArea;

    private readonly RichLabel mQuestDescLabel;

    private readonly Label mQuestDescTemplateLabel;

    private readonly ListBox _questList;

    private readonly Label mQuestStatus;

    //Controls
    private readonly WindowControl mQuestsWindow;

    private readonly Label mQuestTitle;

    private readonly Button mQuitButton;

    private QuestDescriptor mSelectedQuest;

    //Init
    public QuestsWindow(Canvas gameCanvas)
    {
        mQuestsWindow = new WindowControl(gameCanvas, Strings.QuestLog.Title, false, "QuestsWindow");
        mQuestsWindow.DisableResizing();

        _questList = new ListBox(mQuestsWindow, "QuestList");
        _questList.EnableScroll(false, true);

        mQuestTitle = new Label(mQuestsWindow, "QuestTitle");
        mQuestTitle.SetText("");

        mQuestStatus = new Label(mQuestsWindow, "QuestStatus");
        mQuestStatus.SetText("");

        mQuestDescArea = new ScrollControl(mQuestsWindow, "QuestDescription");
        mQuestDescArea.EnableScroll(false, true); // Enable vertical scrolling explicitely

        mQuestDescTemplateLabel = new Label(mQuestsWindow, "QuestDescriptionTemplate");

        mQuestDescLabel = new RichLabel(mQuestDescArea);

        mBackButton = new Button(mQuestsWindow, "BackButton");
        mBackButton.Text = Strings.QuestLog.Back;
        mBackButton.Clicked += _backButton_Clicked;

        mQuitButton = new Button(mQuestsWindow, "AbandonQuestButton");
        mQuitButton.SetText(Strings.QuestLog.Abandon);
        mQuitButton.Clicked += _quitButton_Clicked;

        mQuestsWindow.LoadJsonUi(GameContentManager.UI.InGame, Graphics.Renderer.GetResolutionString());

        // Override stupid decisions in the JSON
        _questList.IsDisabled = false;
        _questList.IsVisibleInTree = true;
    }

    private void _quitButton_Clicked(Base sender, MouseButtonState arguments)
    {
        if (mSelectedQuest != null)
        {
            _ = new InputBox(
                title: Strings.QuestLog.AbandonTitle.ToString(mSelectedQuest.Name),
                prompt: Strings.QuestLog.AbandonPrompt.ToString(mSelectedQuest.Name),
                inputType: InputType.YesNo,
                userData: mSelectedQuest.Id,
                onSubmit: (s, e) =>
                {
                    if (s is InputBox inputBox && inputBox.UserData is Guid questId)
                    {
                        PacketSender.SendAbandonQuest(questId);
                    }
                }
            );
        }
    }

    void AbandonQuest(object sender, EventArgs e)
    {
        PacketSender.SendAbandonQuest((Guid) ((InputBox) sender).UserData);
    }

    private void _backButton_Clicked(Base sender, MouseButtonState arguments)
    {
        mSelectedQuest = null;
        UpdateSelectedQuest();
    }

    private bool _shouldUpdateList;

    public void Update(bool shouldUpdateList)
    {
        if (!mQuestsWindow.IsVisibleInTree)
        {
            _shouldUpdateList |= shouldUpdateList;
            return;
        }

        UpdateInternal(shouldUpdateList);
    }

    private void UpdateInternal(bool shouldUpdateList)
    {
        if (shouldUpdateList)
        {
            UpdateQuestList();
            UpdateSelectedQuest();
        }

        if (mQuestsWindow.IsHidden)
        {
            return;
        }

        if (mSelectedQuest != null)
        {
            if (Globals.Me.QuestProgress.ContainsKey(mSelectedQuest.Id))
            {
                if (Globals.Me.QuestProgress[mSelectedQuest.Id].Completed &&
                    Globals.Me.QuestProgress[mSelectedQuest.Id].TaskId == Guid.Empty)
                {
                    //Completed
                    if (!mSelectedQuest.LogAfterComplete)
                    {
                        mSelectedQuest = null;
                        UpdateSelectedQuest();
                    }

                    return;
                }
                else
                {
                    if (Globals.Me.QuestProgress[mSelectedQuest.Id].TaskId == Guid.Empty)
                    {
                        //Not Started
                        if (!mSelectedQuest.LogBeforeOffer)
                        {
                            mSelectedQuest = null;
                            UpdateSelectedQuest();
                        }
                    }

                    return;
                }
            }

            if (!mSelectedQuest.LogBeforeOffer)
            {
                mSelectedQuest = null;
                UpdateSelectedQuest();
            }
        }
    }

    private void UpdateQuestList()
    {
        _questList.RemoveAllRows();
        if (Globals.Me != null)
        {
            var quests = QuestDescriptor.Lookup.Values;

            var dict = new Dictionary<string, List<Tuple<QuestDescriptor, int, Color>>>();

            foreach (QuestDescriptor quest in quests)
            {
                if (quest != null)
                {
                    AddQuestToDict(dict, quest);
                }
            }


            foreach (var category in Options.Instance.Quest.Categories)
            {
                if (dict.ContainsKey(category))
                {
                    AddCategoryToList(category, Color.White);
                    var sortedList = dict[category].OrderBy(l => l.Item2).ThenBy(l => l.Item1.OrderValue).ToList();
                    foreach (var qst in sortedList)
                    {
                        AddQuestToList(qst.Item1.Name, qst.Item3, qst.Item1.Id, true);
                    }
                }
            }

            if (dict.ContainsKey(""))
            {
                var sortedList = dict[""].OrderBy(l => l.Item2).ThenBy(l => l.Item1.OrderValue).ToList();
                foreach (var qst in sortedList)
                {
                    AddQuestToList(qst.Item1.Name, qst.Item3, qst.Item1.Id, false);
                }
            }

        }
    }

    private void AddQuestToDict(Dictionary<string, List<Tuple<QuestDescriptor, int, Color>>> dict, QuestDescriptor quest)
    {
        var category = string.Empty;
        var add = false;
        var color = Color.White;
        var orderVal = -1;
        if (Globals.Me.QuestProgress.ContainsKey(quest.Id))
        {
            if (Globals.Me.QuestProgress[quest.Id].TaskId != Guid.Empty)
            {
                add = true;
                category = !TextUtils.IsNone(quest.InProgressCategory) ? quest.InProgressCategory : "";
                color = CustomColors.QuestWindow.InProgress;
                orderVal = 1;
            }
            else
            {
                if (Globals.Me.QuestProgress[quest.Id].Completed)
                {
                    if (quest.LogAfterComplete)
                    {
                        add = true;
                        category = !TextUtils.IsNone(quest.CompletedCategory) ? quest.CompletedCategory : "";
                        color = CustomColors.QuestWindow.Completed;
                        orderVal = 3;
                    }
                }
                else
                {
                    if (quest.LogBeforeOffer && !Globals.Me.HiddenQuests.Contains(quest.Id))
                    {
                        add = true;
                        category = !TextUtils.IsNone(quest.UnstartedCategory) ? quest.UnstartedCategory : "";
                        color = CustomColors.QuestWindow.NotStarted;
                        orderVal = 2;
                    }
                }
            }
        }
        else
        {
            if (quest.LogBeforeOffer && !Globals.Me.HiddenQuests.Contains(quest.Id))
            {
                add = true;
                category = !TextUtils.IsNone(quest.UnstartedCategory) ? quest.UnstartedCategory : "";
                color = CustomColors.QuestWindow.NotStarted;
                orderVal = 2;
            }
        }

        if (add)
        {
            if (!dict.ContainsKey(category))
            {
                dict.Add(category, new List<Tuple<QuestDescriptor, int, Color>>());
            }

            dict[category].Add(new Tuple<QuestDescriptor, int, Color>(quest, orderVal, color));
        }
    }

    private void AddQuestToList(string name, Color clr, Guid questId, bool indented = true)
    {
        // Traduzir o nome da quest na lista usando ID
        var key = $"QUEST_NAME_{questId}";
        string translatedName;
        if (TranslationService.Instance.TryGetCachedById(key, out var cached))
        {
            translatedName = cached;
        }
        else if (TranslationService.Instance.TryGetCached(name, out cached))
        {
            translatedName = cached;
        }
        else
        {
            translatedName = name;
        }
        
        var item = _questList.AddRow((indented ? "\t\t\t" : "") + translatedName);
        item.UserData = questId;
        item.Clicked += QuestListItem_Clicked;
        item.Selected += Item_Selected;
        item.SetTextColor(clr);
        item.RenderColor = new Color(50, 255, 255, 255);
    }

    private void AddCategoryToList(string name, Color clr)
    {
        var item = _questList.AddRow(name);
        item.MouseInputEnabled = false;
        item.SetTextColor(clr);
        item.RenderColor = new Color(0, 255, 255, 255);
    }

    private void Item_Selected(Base sender, ItemSelectedEventArgs arguments)
    {
        _questList.UnselectAll();
    }

    private void QuestListItem_Clicked(Base sender, MouseButtonState arguments)
    {
        if (sender.UserData is not Guid questId)
        {
            return;
        }

        if (!QuestDescriptor.TryGet(questId, out var questDescriptor))
        {
            _questList.UnselectAll();
            return;
        }

        mSelectedQuest = questDescriptor;
        UpdateSelectedQuest();
    }

    private string GetTranslated(string text)
    {
        if (TranslationService.Instance.TryGetCached(text, out var translated))
        {
            return translated;
        }
        return text;
    }

    private string GetTranslatedQuestField(Guid questId, string fieldName, string fallbackText)
    {
        // Try to get translation using the structured key
        var key = $"QUEST_{fieldName}_{questId}";
        if (TranslationService.Instance.TryGetCachedById(key, out var translated))
        {
            return translated;
        }
        
        // Fallback to direct text translation
        if (TranslationService.Instance.TryGetCached(fallbackText, out translated))
        {
            return translated;
        }
        
        return fallbackText;
    }

    private string GetTranslatedTaskDescription(Guid questId, Guid taskId, string fallbackText)
    {
        // Try to get translation using the structured key
        var key = $"QUEST_TASK_{questId}_{taskId}";
        if (TranslationService.Instance.TryGetCachedById(key, out var translated))
        {
            return translated;
        }
        
        // Fallback to direct text translation
        if (TranslationService.Instance.TryGetCached(fallbackText, out translated))
        {
            return translated;
        }
        
        return fallbackText;
    }

    private void UpdateSelectedQuest()
    {
        // Refresh quest descriptor reference to ensure we have the latest data
        if (mSelectedQuest != null && QuestDescriptor.TryGet(mSelectedQuest.Id, out var freshDescriptor))
        {
            mSelectedQuest = freshDescriptor;
        }

        if (mSelectedQuest == null)
        {
            _questList.Show();
            mQuestTitle.Hide();
            mQuestDescArea.Hide();
            mQuestStatus.Hide();
            mBackButton.Hide();
            mQuitButton.Hide();
        }
        else
        {
            mQuestDescLabel.ClearText();
            mQuitButton.IsDisabled = true;
            ListBoxRow rw;
            string[] myText = null;
            var taskString = new List<string>();
            if (Globals.Me.QuestProgress.ContainsKey(mSelectedQuest.Id))
            {
                if (Globals.Me.QuestProgress[mSelectedQuest.Id].TaskId != Guid.Empty)
                {
                    //In Progress
                    mQuestStatus.SetText(Strings.QuestLog.InProgress);
                    mQuestStatus.SetTextColor(CustomColors.QuestWindow.InProgress, ComponentState.Normal);
                    mQuestDescTemplateLabel.SetTextColor(CustomColors.QuestWindow.QuestDesc, ComponentState.Normal);

                    if (mSelectedQuest.InProgressDescription.Length > 0)
                    {
                        var translatedDesc = GetTranslatedQuestField(mSelectedQuest.Id, "INPROG", mSelectedQuest.InProgressDescription);
                        mQuestDescLabel.AddText(translatedDesc, mQuestDescTemplateLabel);

                        mQuestDescLabel.AddLineBreak();
                        mQuestDescLabel.AddLineBreak();
                    }

                    mQuestDescLabel.AddText(Strings.QuestLog.CurrentTask, mQuestDescTemplateLabel);

                    mQuestDescLabel.AddLineBreak();
                    for (var i = 0; i < mSelectedQuest.Tasks.Count; i++)
                    {
                        if (mSelectedQuest.Tasks[i].Id == Globals.Me.QuestProgress[mSelectedQuest.Id].TaskId)
                        {
                            if (mSelectedQuest.Tasks[i].Description.Length > 0)
                            {
                                var translatedTaskDesc = GetTranslatedTaskDescription(
                                    mSelectedQuest.Id, 
                                    mSelectedQuest.Tasks[i].Id, 
                                    mSelectedQuest.Tasks[i].Description);
                                mQuestDescLabel.AddText(translatedTaskDesc, mQuestDescTemplateLabel);

                                mQuestDescLabel.AddLineBreak();
                                mQuestDescLabel.AddLineBreak();
                            }

                            if (mSelectedQuest.Tasks[i].Objective == QuestObjective.GatherItems) //Gather Items
                            {
                                mQuestDescLabel.AddText(
                                    Strings.QuestLog.TaskItem.ToString(
                                        Globals.Me.QuestProgress[mSelectedQuest.Id].TaskProgress,
                                        mSelectedQuest.Tasks[i].Quantity,
                                        ItemDescriptor.GetName(mSelectedQuest.Tasks[i].TargetId)
                                    ), mQuestDescTemplateLabel
                                );
                            }
                            else if (mSelectedQuest.Tasks[i].Objective == QuestObjective.KillNpcs) //Kill Npcs
                            {
                                mQuestDescLabel.AddText(
                                    Strings.QuestLog.TaskNpc.ToString(
                                        Globals.Me.QuestProgress[mSelectedQuest.Id].TaskProgress,
                                        mSelectedQuest.Tasks[i].Quantity,
                                        NPCDescriptor.GetName(mSelectedQuest.Tasks[i].TargetId)
                                    ), mQuestDescTemplateLabel
                                );
                            }
                        }
                    }

                    mQuitButton.IsDisabled = !mSelectedQuest.Quitable;
                }
                else
                {
                    if (Globals.Me.QuestProgress[mSelectedQuest.Id].Completed)
                    {
                        //Completed
                        if (mSelectedQuest.LogAfterComplete)
                        {
                            mQuestStatus.SetText(Strings.QuestLog.Completed);
                            mQuestStatus.SetTextColor(CustomColors.QuestWindow.Completed, ComponentState.Normal);
                            var translatedEndDesc = GetTranslatedQuestField(mSelectedQuest.Id, "END", mSelectedQuest.EndDescription);
                            mQuestDescLabel.AddText(translatedEndDesc, mQuestDescTemplateLabel);
                        }
                    }
                    else
                    {
                        //Not Started
                        if (mSelectedQuest.LogBeforeOffer)
                        {
                            mQuestStatus.SetText(Strings.QuestLog.NotStarted);
                            mQuestStatus.SetTextColor(CustomColors.QuestWindow.NotStarted, ComponentState.Normal);
                            var translatedBeforeDesc = GetTranslatedQuestField(mSelectedQuest.Id, "BEFORE", mSelectedQuest.BeforeDescription);
                            mQuestDescLabel.AddText(translatedBeforeDesc, mQuestDescTemplateLabel);

                            mQuitButton?.Hide();
                        }
                    }
                }
            }
            else
            {
                //Not Started
                if (mSelectedQuest.LogBeforeOffer)
                {
                    mQuestStatus.SetText(Strings.QuestLog.NotStarted);
                    mQuestStatus.SetTextColor(CustomColors.QuestWindow.NotStarted, ComponentState.Normal);
                    var translatedBeforeDesc = GetTranslatedQuestField(mSelectedQuest.Id, "BEFORE", mSelectedQuest.BeforeDescription);
                    mQuestDescLabel.AddText(translatedBeforeDesc, mQuestDescTemplateLabel);
                }
            }

            _questList.Hide();
            mQuestTitle.IsHidden = false;
            // Traduzir o título da quest usando ID
            var translatedName = GetTranslatedQuestField(mSelectedQuest.Id, "NAME", mSelectedQuest.Name);
            mQuestTitle.Text = translatedName;
            
            // Fix layout bug causing text cut off sometimes
            mQuestDescLabel.Width = mQuestDescArea.Width - mQuestDescArea.VerticalScrollBar.Width;
            mQuestDescLabel.SizeToChildren(false, true);
            
            mQuestDescArea.IsHidden = false;
            mQuestStatus.Show();
            mBackButton.Show();
            mQuitButton.Show();
        }
    }

    public void Show()
    {
        if (_shouldUpdateList)
        {
            UpdateInternal(_shouldUpdateList);
            _shouldUpdateList = false;
        }

        mQuestsWindow.IsHidden = false;
    }

    public bool IsVisible()
    {
        return !mQuestsWindow.IsHidden;
    }

    public void Hide()
    {
        mQuestsWindow.IsHidden = true;
        mSelectedQuest = null;
    }

}
