using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows.Forms;

// move into diff file
// https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/loot.json

namespace HextechButBetter
{
    public partial class HextechButBetterForm : Form
    {

        LeagueConnection lc;

        private const String DISENCHANT_TEXT = "Disenchant";
        private const String UPGRADE_TEXT = "Upgrade";
        private const String FORGE_EMOTE_TEXT = "Forge into Unowned Permanent";
        private const String FORGE_EGG_TEXT = "Forge into egg";

        Dictionary<String, List<JsonObject>> map = new Dictionary<string, List<JsonObject>>();
        Dictionary<String, String> processTypes = new Dictionary<string, string>
        {
            { DISENCHANT_TEXT, "DISENCHANT" },
            { UPGRADE_TEXT, "UPGRADE" },
            { FORGE_EMOTE_TEXT, "FORGE" },
            { FORGE_EGG_TEXT, "FORGE" },
        };

        private Dictionary<String, List<String>> preparedPosts = new Dictionary<string, List<String>>();

        private JsonObject lootNames = null;
        private JsonArray recipes = null;
        
        private List<Tuple<string, JsonObject>> lootNameAndRecipeName = new List<Tuple<string, JsonObject>>();
        
        enum LootType {Unknown, Champion, Skin, Emote, Wardskin, Icon, Companion, Eternals, Chest};
        private LootType currentLoot = LootType.Unknown;
        
        public HextechButBetterForm()
        {
            InitializeComponent();
        }

        private void HextechButBetterForm_Load(object sender, EventArgs e)
        {
            lc = new LeagueConnection();
            getLatestReleaseDate(repoUrlButton);
        }

        private async void loadChampionsButton_Click(object sender, EventArgs e)
        {
            await printContent("CHAMPION", LootType.Champion);
        }

        private async void loadSkinsButton_Click(object sender, EventArgs e)
        {
            printContent("SKIN", LootType.Skin);
        }

        private async void loadEmotesButton_Click(object sender, EventArgs e)
        {
            printContent("EMOTE", LootType.Emote);
        }

        private async void loadWardsButton_Click(object sender, EventArgs e)
        {
            printContent("WARDSKIN", LootType.Wardskin);
        }

        private async void loadIconsButton_Click(object sender, EventArgs e)
        {
            printContent("SUMMONERICON", LootType.Icon);
        }

        private async void loadCompanionsButton_Click(object sender, EventArgs e)
        {
            printContent("COMPANION", LootType.Companion);
        }

        private async void loadEternalsButton_Click(object sender, EventArgs e)
        {
            printContent("ETERNALS", LootType.Eternals);
        }

        private async void loadChestsButton_Click(object sender, EventArgs e)
        {
            printContent("CHEST", LootType.Chest);
        }

        private void editMessageLabel(String msg)
        {
            messageLabel.Text = msg;
        }

        private Boolean checkIfLeagueIsConnected()
        {
            if (!lc.IsConnected)
            {
                editMessageLabel("Not connected to League Client! You need to log in first and wait few seconds. Try running app as admin too.");
                return false;
            }
            else return true;
        }

        private async Task refreshData()
        {
            map.Clear();
            JsonObject playerLoot = (JsonObject)await lc.Get("/lol-loot/v1/player-loot-map");
            if (playerLoot == null) return;
            playerLoot.Remove("");
            
            for (int i = 0; i < playerLoot.Count; i++)
            {
                JsonObject item = (JsonObject)playerLoot[i];
                String category = (String)item["displayCategories"];
                if (category.Equals("")) category = "Unknown";
                if (map.ContainsKey(category))
                {
                    map[category].Add(item);
                }
                else
                {
                    List<JsonObject> newCategory = new List<JsonObject>();
                    newCategory.Add(item);
                    map.Add(category, newCategory);
                }
            }
        }

        private void clearOutputPanel()
        {
            outputPanel.Controls.Clear();
        }

        private async Task printContent(string contentType, LootType lootType)
        {
            
            if (!checkIfLeagueIsConnected()) return;
            await refreshData();
            
            if (!printData(contentType))
                return;
            currentLoot = lootType;
            fillProcessType();
        }

        private void fillProcessType()
        {
            clearProcessType();
            switch (currentLoot)
            {
                case LootType.Companion:
                    processType.Items.Insert(0, FORGE_EGG_TEXT);
                    break;
                case LootType.Emote:
                    processType.Items.Insert(0, "");
                    processType.Items.Insert(1, DISENCHANT_TEXT);
                    processType.Items.Insert(2, FORGE_EMOTE_TEXT);
                    break;
                default:
                    processType.Items.Insert(0, "");
                    processType.Items.Insert(1, DISENCHANT_TEXT);
                    processType.Items.Insert(2, UPGRADE_TEXT);
                    break;
            }
        }

        private void clearProcessType()
        {
            processType.Items.Clear();
            processType.ResetText();
        }

        private bool printData(String type)
        {
            clearOutputPanel();
            if (!map.ContainsKey(type))
            {
                editMessageLabel("No data found.");
                currentLoot = LootType.Unknown;
                return false;
            }
            if (type == "CHEST")
            {
                ComboBox chestsComboBox = new ComboBox
                {
                    Name = nameof(chestsComboBox),
                    Width = 200,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                };
                chestsComboBox.SelectedValueChanged += chestComboBoxChanged;
                chestsComboBox.Items.Add("Select material ("+ map[type].Count + " found)");
                outputPanel.Controls.Add(chestsComboBox);
                chestsComboBox.SelectedIndex = 0;

                int index = 1;
                foreach (JsonObject item in map[type])
                {
                    Int64 count = 0;
                    count = (Int64) item["count"];
                    String lootId = (String) item["lootId"];
                    String lootName = (String) item["lootName"];
                    String itemName = (String) item["localizedName"];
                    if (itemName.Equals(""))
                    {
                        if (lootNames == null)
                            getNamesFromCommunityDragon();
                        if (lootName.StartsWith("CHAMPION_TOKEN_"))
                            itemName = (String) lootNames["loot_name_" + lootName.ToLower()] + (String) item["itemDesc"];
                        else if (lootName.EndsWith("MATERIAL_key_fragment"))
                            itemName = (String) lootNames["loot_name_" + lootName.ToLower() + "[other]"] + (String) item["itemDesc"];
                        else
                            itemName = (String) lootNames["loot_name_" + lootId.ToLower()];
                    }
                    chestsComboBox.Items.Insert(index, new Material(count, itemName));
                    index++;
                }
                NumericUpDown chestsRepeatNumericUpDown = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 999999,
                    Name = nameof(chestsRepeatNumericUpDown)
                };
                chestsComboBox.SelectedIndexChanged += chestsComboBoxIndexChanged;

                void chestsComboBoxIndexChanged(object sender, EventArgs e)
                {
                    var item = chestsComboBox.SelectedItem;
                    if (item is not Material material)
                        return;

                    chestsRepeatNumericUpDown.Maximum = material.Count;
                    chestsRepeatNumericUpDown.Value = material.Count;
                }

                Label label = new Label
                {
                    Text = "Repeat: ",
                    AutoSize = true
                };

                ComboBox chestsRecipeComboBox = new ComboBox
                {
                    Name = nameof(chestsRecipeComboBox),
                    Width = 200,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                };
                chestsRecipeComboBox.Items.Insert(0, "Select recipe (0 found)");

                outputPanel.Controls.Add(label);
                outputPanel.Controls.Add(chestsRepeatNumericUpDown);
                outputPanel.Controls.Add(chestsRecipeComboBox);
                chestsRecipeComboBox.SelectedIndex = 0;
            }
            else
            {
                var labelWidth = 270;
                var textBoxWidth = 50;
                var textAlign = ContentAlignment.MiddleLeft;
                
                Int64 numberOfShards = 0;
                foreach (JsonObject item in map[type])
                {
                    Int64 count = (Int64) item["count"];
                    numberOfShards += count;
                }

                Label uniqueLabel = new Label();
                uniqueLabel.Text = "Unique shards";
                uniqueLabel.Width = labelWidth;
                uniqueLabel.TextAlign = textAlign;
                
                NumericUpDown uniqueNumbericUpDown = new NumericUpDown();
                uniqueNumbericUpDown.Minimum = map[type].Count;
                uniqueNumbericUpDown.Maximum = map[type].Count;
                uniqueNumbericUpDown.Width = textBoxWidth;

                Label totalLabel = new Label();
                totalLabel.Text = "All shards";
                totalLabel.Width = labelWidth;
                totalLabel.TextAlign = textAlign;
                
                NumericUpDown totalNumbericUpDown = new NumericUpDown();
                totalNumbericUpDown.Minimum = numberOfShards;
                totalNumbericUpDown.Maximum = numberOfShards;
                totalNumbericUpDown.Width = textBoxWidth;
                
                outputPanel.Controls.Add(uniqueNumbericUpDown);
                outputPanel.Controls.Add(uniqueLabel);
                
                outputPanel.Controls.Add(totalNumbericUpDown);
                outputPanel.Controls.Add(totalLabel);
                
                foreach (JsonObject item in map[type])
                {
                    Int64 count = 0;
                    count = (Int64) item["count"];
                    String itemDesc = (String) item["localizedName"];
                    if (itemDesc.Equals("")) itemDesc = (String) item["itemDesc"];
                    String itemType = (String) item["type"];
                    if (itemType == "SKIN_RENTAL") itemDesc += " (Shard)";
                    else if (itemType == "SKIN") itemDesc += " (Permanent)";
                    String lootId = (String) item["lootId"];
                    NumericUpDown numericUpDown = new NumericUpDown();
                    numericUpDown.Value = count;
                    numericUpDown.Minimum = 0;
                    numericUpDown.Maximum = count;
                    numericUpDown.Name = lootId;
                    numericUpDown.Width = textBoxWidth;

                    Label label = new Label();
                    label.Text = count + "x " + itemDesc;
                    label.Width = labelWidth;
                    label.TextAlign = textAlign;

                    outputPanel.Controls.Add(numericUpDown);
                    outputPanel.Controls.Add(label);
                }
            }
            editMessageLabel("Loaded.");
            return true;
        }

        private sealed class Material
        {
            public long Count { get; }
            public string Name { get; }

            public Material(long count, string name)
            {
                Count = count;
                Name = name;
            }

            public override string ToString()
            {
                return $"{Count}x {Name}";
            }
        }

        private void processButton_Click(object sender, EventArgs e)
        {
            switch (currentLoot)
            {
                case LootType.Champion:
                    processLoot(map["CHAMPION"]);
                    break;
                case LootType.Skin:
                    processLoot(map["SKIN"]);
                    break;
                case LootType.Emote:
                    processLoot(map["EMOTE"]);
                    break;
                case LootType.Wardskin:
                    processLoot(map["WARDSKIN"]);
                    break;
                case LootType.Icon:
                    processLoot(map["SUMMONERICON"]);
                    break;
                case LootType.Eternals:
                    processLoot(map["ETERNALS"]);
                    break;
                case LootType.Companion:
                    processLoot(map["COMPANION"]);
                    break;
                case LootType.Chest:
                    processLootChest();
                    break;
                default:
                    MessageBox.Show("Load data before processing");
                    return;
            }
        }

        private async void processLoot(List<JsonObject> items)
        {
                
            String recipeType = processType.Text;
            if (recipeType.Equals(""))
            {
                MessageBox.Show("Select processing type");
                return;
            }
            String recipeTypeFull = processTypes[recipeType];
            
            foreach (JsonObject item in items)
            {
                String lootId = (String) item["lootId"];
                NumericUpDown numericUpDown = (NumericUpDown) outputPanel.Controls[lootId];
                Decimal repeat = numericUpDown.Value;
                if (repeat == 0) continue;
                
                recipes = (JsonArray) await lc.Get("/lol-loot/v1/recipes/initial-item/" + item["lootId"]);
                foreach (JsonObject recipe in recipes)
                {
                    if (recipe["type"].Equals(recipeTypeFull)) {
                        String recipeName = (String) recipe["recipeName"];
                        String url = "/lol-loot/v1/recipes/" + recipeName + "/craft?repeat=" + repeat;
                        String body = "[\"" + lootId + "\"]";
                        generatePosts(url, body);
                        //MessageBox.Show(url + ", " + body + "\n" + "Processed!");
                        break;
                    }
                }
                
            }
            executePosts();
            MessageBox.Show("Processed!");
        }

        private async void processLootChest()
        {
            ComboBox chestsComboBox = (ComboBox) outputPanel.Controls["chestsComboBox"];
            if (chestsComboBox.SelectedIndex <= 0)
            {
                MessageBox.Show("Select material before processing");
                return;
            }
            ComboBox chestsRecipeComboBox = (ComboBox) outputPanel.Controls["chestsRecipeComboBox"];
            if (chestsRecipeComboBox.SelectedIndex <= 0)
            {
                MessageBox.Show("Select recipe before processing");
                return;
            }
            NumericUpDown chestsRepeatNumericUpDown = (NumericUpDown) outputPanel.Controls["chestsRepeatNumericUpDown"];
            if (chestsRepeatNumericUpDown.Value <= 0)
            {
                MessageBox.Show("Repeat at least once");
                return;
            }
            JsonObject recipe = lootNameAndRecipeName[chestsRecipeComboBox.SelectedIndex - 1].Item2;
            JsonArray slots = (JsonArray) recipe["slots"];
            String body = "[";
            
            foreach (JsonObject slot in slots)
            {
                JsonArray lootIds = (JsonArray)slot["lootIds"];
                if (lootIds.Count != 0)
                {
                    foreach (String lootId in lootIds)
                    {
                        body += "\"" + lootId + "\",";
                    }
                }
                else
                {
                    body += "\"MATERIAL_key\",";
                }
            }
            body = body.Remove(body.Length - 1);
            body += "]";

            String url = "/lol-loot/v1/recipes/" +
                         recipe["recipeName"] +
                         "/craft?repeat=" + chestsRepeatNumericUpDown.Value;
            
            await lc.Post(url, body);

            MessageBox.Show(url + ", " + body + "\n" + "Processed!");
        }

        private void getNamesFromCommunityDragon()
        {
            using (WebClient wc = new WebClient())
            {
                var json = wc.DownloadString("https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-loot/global/en_us/trans.json");
                Console.Out.WriteLine(json);
                lootNames = (JsonObject) SimpleJson.DeserializeObject(json);
            }
        }

        private async void chestComboBoxChanged(object sender, EventArgs e)
        {
            ComboBox chestsComboBox = (ComboBox) outputPanel.Controls[nameof(chestsComboBox)];
            if (chestsComboBox.SelectedIndex <= 0) return;

            ComboBox chestsRecipeComboBox = (ComboBox) outputPanel.Controls[nameof(chestsRecipeComboBox)];
            chestsRecipeComboBox.Items.Clear();
            chestsRecipeComboBox.ResetText();
            JsonObject item = map["CHEST"][chestsComboBox.SelectedIndex-1];
            recipes = (JsonArray) await lc.Get("/lol-loot/v1/recipes/initial-item/" + item["lootId"]);
            chestsRecipeComboBox.Items.Add("Select recipe (" + recipes.Count + " found)");
            lootNameAndRecipeName = new List<Tuple<string, JsonObject>>();
            foreach (JsonObject recipe in recipes)
            {
                String recipeName = (String) recipe["contextMenuText"];
                if (recipeName.Equals(""))
                {
                    recipeName = (String) recipe["description"];
                }
                lootNameAndRecipeName.Add(new Tuple<string, JsonObject>(recipeName, recipe));
            }
            lootNameAndRecipeName = lootNameAndRecipeName.OrderBy(t => t.Item1).ToList();
            int index = 1;
            foreach (var recipe in lootNameAndRecipeName)
            {
                chestsRecipeComboBox.Items.Insert(index, recipe.Item1);
                chestsRecipeComboBox.SelectedIndex = 0;
                index++;
            }
        }

        private void generatePosts(String url, String body)
        {
            if (!preparedPosts.ContainsKey(url))
            {
                preparedPosts.Add(url, new List<string>());
            }
            preparedPosts[url].Add(body);
        }

        private void executePosts()
        {
            foreach (KeyValuePair<string, List<String>> data in preparedPosts)
            {
                foreach (String body in data.Value)
                    lc.Post(data.Key, body);
            }
            preparedPosts.Clear();
        }

        private async void getLatestReleaseDate(Button button)
        {
            using (HttpClient client = new HttpClient())
            {
                var userAgentHeader = new ProductInfoHeaderValue("HextechButBetterHttpClient", "1.0");
                client.DefaultRequestHeaders.UserAgent.Add(userAgentHeader);
                string latestReleaseString = await client.GetStringAsync("https://api.github.com/repos/MaciejGorczyca/HextechButBetter/releases/latest");
                var latestReleaseJson = (JsonObject) SimpleJson.DeserializeObject(latestReleaseString);
                var latestReleasePublishedDate = "\nLatest release date: " + (String) latestReleaseJson["published_at"];
                button.Text += latestReleasePublishedDate;
            }
            
        }

        private void legalNoteButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("HextechButBetter isn’t endorsed by Riot Games and doesn’t reflect the views or opinions of Riot Games or anyone officially involved in producing or managing League of Legends. League of Legends and Riot Games are trademarks or registered trademarks of Riot Games, Inc. League of Legends © Riot Games, Inc.");
        }

        private void donateButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
@"The browser will now open.
                            
Thank you for considering donation!
The software is completely free to use for everyone.
If you wish to thank me and help me back, feel free to send me even the smallest possible amount.
                            
I will greatly appreciate your goodwill!");
            System.Diagnostics.Process.Start("https://www.paypal.me/CoUsTme/1EUR");
        }

        private void RepoUrlButton_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/MaciejGorczyca/HextechButBetter/releases/latest");
        }

        private void ChallengerAreEvilUrlButton_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/MaciejGorczyca/ChallengesAreEvil");
        }
    }
}