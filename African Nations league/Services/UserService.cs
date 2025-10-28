using African_Nations_league.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace African_Nations_league.Services
{
    public class UserService
    {
        private readonly IMongoCollection<User> _users;

        public UserService(IMongoClient mongoClient)
        {
            var database = mongoClient.GetDatabase("african_nations_db");
            _users = database.GetCollection<User>("users");
        }

        // 🔹 Récupérer tous les utilisateurs
        public async Task<List<User>> GetAllAsync()
        {
            return await _users.Find(_ => true).ToListAsync();
        }

        // 🔹 Récupérer un utilisateur par email
        public async Task<User> GetByEmailAsync(string email)
        {
            return await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        }

        // 🔹 Créer un nouvel utilisateur
        public async Task CreateUserAsync(User user)
        {
            await _users.InsertOneAsync(user);
        }

        // 🔹 Mettre à jour un utilisateur existant
        public async Task UpdateUserAsync(User user)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
            await _users.ReplaceOneAsync(filter, user);
        }

        // 🔹 Supprimer un utilisateur
        public async Task DeleteUserAsync(string id)
        {
            await _users.DeleteOneAsync(u => u.Id == id);
        }

        // 🔹 Compter le nombre total d’utilisateurs
        public async Task<long> CountAsync()
        {
            return await _users.CountDocumentsAsync(_ => true);
        }
    }
}
