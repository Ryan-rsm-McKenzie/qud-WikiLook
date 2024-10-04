#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Text;
using ConsoleLib.Console;
using HarmonyLib;
using Iterator;
using Qud.UI;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace WikiLook
{
	[HarmonyPatch(typeof(Description))]
	[HarmonyPatch(nameof(Description.HandleEvent))]
	[HarmonyPatch(new Type[] { typeof(InventoryActionEvent) })]
	public class Description_HandleEvent
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			var matcher = new CodeMatcher(instructions);
			matcher
				.Start()
				.MatchStartForward(new CodeMatch[] {
					new(OpCodes.Call, AccessTools.Method(
						type: typeof(Options),
						name: "get_ModernUI",
						parameters: new Type[] {}
					)),
					new(OpCodes.Brfalse_S),
				});
			if (matcher.IsInvalid) {
				Logger.buildLog.Error("Failed to match start of ui block");
				return instructions;
			}
			int start = matcher.Pos;
			var labels = matcher.Labels;

			matcher.MatchStartForward(new CodeMatch[] {
				new(OpCodes.Ldarg_1),
				new(OpCodes.Ldfld, AccessTools.Field(
					type: typeof(IActOnItemEvent),
					name: nameof(IActOnItemEvent.Actor)
				)),
				new(OpCodes.Ldstr, "LookedAt"),
				new(OpCodes.Ldstr, "Object"),
				new(OpCodes.Ldarg_0),
				new(OpCodes.Callvirt, AccessTools.Method(
					type: typeof(IPart),
					name: "get_ParentObject",
					parameters: new Type[] {}
				)),
				new(OpCodes.Call, AccessTools.Method(
					type: typeof(Event),
					name: nameof(Event.New),
					parameters: new Type[] { typeof(string), typeof(string), typeof(object), }
				)),
				new(OpCodes.Callvirt, AccessTools.Method(
					type: typeof(GameObject),
					name: nameof(GameObject.FireEvent),
					parameters: new Type[] { typeof(Event) }
				)),
				new(OpCodes.Pop),
			});
			if (matcher.IsInvalid) {
				Logger.buildLog.Error("failed to match end of ui block");
				return instructions;
			}
			int end = matcher.Pos;

			var patch = new CodeInstruction[] {
				new(OpCodes.Nop) { labels = labels },
				new(OpCodes.Ldarg_0), // Description
				new(OpCodes.Ldloc_0), // Look.TooltipInformation
				new(OpCodes.Ldloc_1), // StringBuilder
				new(OpCodes.Ldloc_2), // StringBuilder
				new(OpCodes.Ldloc_3), // List<QudMenuItem>
				new(OpCodes.Call, AccessTools.Method(
					type: typeof(Description_HandleEvent),
					name: nameof(Detour)
				))
			};
			var result = matcher
				.Start()
				.Advance(start)
				.RemoveInstructions(end - start)
				.Insert(patch)
				.Instructions();
			return result;
		}

		private static void Detour(
			Description self,
			Look.TooltipInformation tooltip,
			StringBuilder message,
			StringBuilder title,
			List<QudMenuItem> buttons)
		{
			buttons.Add(new QudMenuItem {
				command = "Wiki",
				hotkey = "W",
				text = "{{W|W}}iki",
			});

			if (Options.ModernUI) {
				string command =
					Popup.NewPopupMessageAsync(
						message: message.ToString(),
						buttons: buttons,
						contextTitle: title.ToString(),
						contextRender: tooltip.IconRenderable)
					.Result
					.command;
				if (command == "Story") {
					BookUI.ShowBookByID(self.ParentObject.Property["Story"]);
				} else if (command == "Wiki") {
					OpenWiki(self.ParentObject);
				}
			} else {
				title.Append("\n\n").Append(message);
				message.Clear();
				var prompts = buttons.Map(x => x.text).Intersperse(", ");
				foreach (string prompt in prompts) {
					message.Append(prompt);
				}
				var input = Popup.ShowBlockPrompt(
					Message: title.ToString(),
					Prompt: message.ToString(),
					Icon: self.ParentObject.RenderForUI(),
					Capitalize: false,
					MuteBackground: true,
					CenterIcon: false,
					RightIcon: true,
					LogMessage: false);
				if (input == Keys.S && buttons.Any(x => x.command == "Story")) {
					BookUI.ShowBookByID(self.ParentObject.Property["Story"]);
				} else if (input == Keys.W) {
					OpenWiki(self.ParentObject);
				}
			}
		}

		private static void OpenWiki(GameObject go)
		{
			string search = WebUtility.UrlEncode(go.BaseDisplayNameStripped);
			string url = $"https://wiki.cavesofqud.com/index.php?search={search}&title=Special%3ASearch&go=Go";
			Process.Start(url);
		}
	}
}
