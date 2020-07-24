using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace KoyakBot
{
	public class Program
	{
		private DiscordSocketClient _discordClient;
		private TelegramBotClient _telegramClient;
		private IConfigurationRoot _configuration;

		public static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

		public async Task MainAsync()
		{
			var builder = new ConfigurationBuilder()
				.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json");

			_configuration = builder.Build();

			_discordClient = new DiscordSocketClient();

			_discordClient.Log += Log;

			await _discordClient.LoginAsync(TokenType.Bot, _configuration["DiscordAPI"]);
			await _discordClient.StartAsync();

			_discordClient.MessageReceived += MessageReceived;
			_discordClient.Ready += Ready;

			_telegramClient = new TelegramBotClient(_configuration["TelegramAPI"]);

			// Block this task until the program is closed.
			await Task.Delay(-1);
		}

		private async Task Ready()
		{
			Console.WriteLine("Bot is ready!");
			ChecklatestMessage();
		}

		private async Task MessageReceived(SocketMessage message)
		{
			if (message.Content == "!ping")
			{
				await message.Channel.SendMessageAsync("Pong!");
			}

			if (message.Content == "!testmsg")
			{
				await message.Channel.SendMessageAsync(await GetDiscordLiveMessage());
			}

			if (message.Content == "!sendtelegram")
			{
				SendMessageToTelegram("Test to telegram succeded!");
			}
		}

		private async void SendMessageToTelegram(string msg)
		{
			await _telegramClient.SendTextMessageAsync(
			  chatId: _configuration["TelegramChatId"],
			  text: msg,
			  parseMode: ParseMode.Default
			);
		}

		private static Timer _timer;
		int count = 0;
		private void ChecklatestMessage()
		{
			_timer = new Timer(_ => TimerCallback(), null, 0, Timeout.Infinite); //in 10 seconds
		}


		private async void TimerCallback()
		{
			_timer.Dispose();

			string msg = await GetDiscordLiveMessage();
			if (!string.IsNullOrEmpty(msg))
			{
				SendMessageToTelegram(msg);
			}
			Console.WriteLine(++count);

			_timer = new Timer(_ => TimerCallback(), null, 1000 * int.Parse(_configuration["SchedulerSeconds"]), Timeout.Infinite); //in 10 seconds
		}

		private Task Log(LogMessage msg)
		{
			Console.WriteLine(msg.ToString());
			return Task.CompletedTask;
		}

		private async Task<string> GetDiscordLiveMessage()
		{
			var server = _discordClient.GetGuild(ulong.Parse(_configuration["DiscordServerId"]));
			var channel = server.GetTextChannel(ulong.Parse(_configuration["DiscordStreamChannelId"]));

			var messages = await channel.GetMessagesAsync(1).FlattenAsync();
			var userMessages = messages.Where(x => x.Author.Id == ulong.Parse(_configuration["DiscordUserAuthorId"]));

			StreamWriter streamWriter = File.AppendText("msgid.txt");
			streamWriter.Dispose();
			string storedId = File.ReadAllText("msgid.txt");

			foreach (IMessage msg in userMessages.ToList())
			{
				if (msg.Id.ToString().Trim() != storedId.Trim())
				{
					if (msg.Embeds.Count > 0)
					{
						using (StreamWriter writer = System.IO.File.CreateText("msgid.txt"))
						{
							writer.WriteLine(msg.Id.ToString());
						}
						return $"{msg.Embeds.First().Title}\n{msg.Embeds.First().Description}";
					}
				}
			}
			return string.Empty;
		}
	}
}
