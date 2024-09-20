using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using signalr.backend.Data;
using signalr.backend.Models;

namespace signalr.backend.Hubs
{
    // On garde en mémoire les connexions actives (clé: email, valeur: userId)
    // Note: Ce n'est pas nécessaire dans le TP
    public static class UserHandler
    {
        public static Dictionary<string, string> UserConnections { get; set; } = new Dictionary<string, string>();
    }

    // L'annotation Authorize fonctionne de la même façon avec SignalR qu'avec Web API
    [Authorize]
    // Le Hub est le type de base des "contrôleurs" de SignalR
    public class ChatHub : Hub
    {
        public ApplicationDbContext _context;

        public IdentityUser CurentUser
        {
            get
            {
                // On récupère le userid à partir du Cookie qui devrait être envoyé automatiquement
                string userid = Context.UserIdentifier!;
                return _context.Users.Single(u => u.Id == userid);
            }
        }

        public ChatHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async override Task OnConnectedAsync()
        {
            UserHandler.UserConnections.Add(CurentUser.Email!, Context.UserIdentifier);

            // TODO: Envoyer des message aux clients pour les mettre à jour
            await Clients.All.SendAsync("UsersList", UserHandler.UserConnections.ToList());
            await Clients.All.SendAsync("ChannelList", _context.Channel.ToList());
        }

        public async override Task OnDisconnectedAsync(Exception? exception)
        {
            // Lors de la fermeture de la connexion, on met à jour notre dictionnary d'utilisateurs connectés
            KeyValuePair<string, string> entrie = UserHandler.UserConnections.SingleOrDefault(uc => uc.Value == Context.UserIdentifier);
            UserHandler.UserConnections.Remove(entrie.Key);

            // TODO: Envoyer un message aux clients pour les mettre à jour
            await Clients.All.SendAsync("UsersList", UserHandler.UserConnections.ToList());
        }

        public async Task CreateChannel(string title)
        {
            _context.Channel.Add(new Channel { Title = title });
            await _context.SaveChangesAsync();

            // TODO: Envoyer un message aux clients pour les mettre à jour
            await Clients.All.SendAsync("ChannelList", _context.Channel.ToList());
            await Clients.All.SendAsync("NewMessage", title + " est créé");
        }

        public async Task DeleteChannel(int channelId)
        {
            Channel channel = _context.Channel.Find(channelId);

            if(channel != null)
            {
                _context.Channel.Remove(channel);
                await _context.SaveChangesAsync();
            }
            string groupName = CreateChannelGroupName(channelId);
            // Envoyer les messages nécessaires aux clients
            await Clients.All.SendAsync("ChannelList", _context.Channel.ToList());
            await Clients.All.SendAsync("NewMessage", "[" + channel.Title + "]" + " est supprimé");
        }

        public async Task JoinChannel(int oldChannelId, int newChannelId)
        {
            string userTag = "[" + CurentUser.Email! + "]";

            // TODO: Faire quitter le vieux canal à l'utilisateur
            if (oldChannelId != 0)
            {
                string oldChanneName = (await _context.Channel.FindAsync(oldChannelId)).Title;
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, oldChanneName);
                await Clients.Group(oldChanneName).SendAsync("NewMessage", userTag + " quitte " + oldChanneName);
            }

            // TODO: Faire joindre le nouveau canal à l'utilisateur

            if (newChannelId != 0)
            {
                string newChanneName = (await _context.Channel.FindAsync(newChannelId)).Title;
                await Groups.AddToGroupAsync(Context.ConnectionId, newChanneName);
                await Clients.Group(newChanneName).SendAsync("NewMessage", userTag + " rejoint " + newChanneName);
            }
        }

        public async Task SendMessage(string message, int channelId, string userId)
        {
            if (userId != null)
            {
                // TODO: Envoyer le message à cet utilisateur
                string userName = (await _context.Users.FindAsync(userId)).UserName;
                await Clients.User(userId).SendAsync("NewMessage", "[" + userName + "] " + message);
            }
            else if (channelId != 0)
            {
                // TODO: Envoyer le message aux utilisateurs connectés à ce canal
                string groupName = (await _context.Channel.FindAsync(channelId)).Title;
                await Clients.Group(groupName).SendAsync("NewMessage", "[" + groupName + "] " + message);
            }
            else
            {
                await Clients.All.SendAsync("NewMessage", "[Tous] " + message);
            }
        }

        private static string CreateChannelGroupName(int channelId)
        {
            return "Channel" + channelId;
        }
    }
}