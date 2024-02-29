namespace ConsoleApp1.DataAccess.Utils;

using Entities;
using Microsoft.EntityFrameworkCore;


public class ApplicationContext : DbContext
{
    public ApplicationContext()
    {
        //Database.EnsureDeleted();
        Database.EnsureCreated();
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("DataSource=..\\..\\..\\Data\\Data.db");
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
        .HasIndex(u => u.Login)
        .IsUnique();
        modelBuilder.Entity<User>()
        .HasIndex(u => u.Email)
        .IsUnique();
    }
    public async Task<User> GeUserByIdAsync(Guid id)
    {

        return await Users.Where(u => u.Id == id).FirstOrDefaultAsync();
    }
    public User GeUserById(Guid id)
    {

        return Users.Where(u => u.Id == id).FirstOrDefault();
    }
    public async Task<User> GeUserByEmailAsync(string email)
    {

        return await Users.Where(u => u.Email == email).FirstOrDefaultAsync();
    }
    public User GeUserByIEmail(string email)
    {
        return Users.Where(u => u.Email == email).FirstOrDefault();
    }
    public async Task<User> GeUserByLoginAsync(string login)
    {

        return await Users.Where(u => u.Login == login).FirstOrDefaultAsync();
    }
    public User GeUserByLogin(string login)
    {

        return Users.Where(u => u.Login == login).FirstOrDefault();
    }
    public bool IsLogged(User user)
    {
        return Users.Where(u => u.Equals(user)).Any();
    }
    public async Task<bool> IsLoggedAsync(User user)
    {
        return await Users.Where(u => u.Equals(user)).AnyAsync();
    }
    public bool IsLogged(string email, string password)
    {
        return Users.Where(u => u.Email.Equals(email) && u.Password.Equals(password)).Any();
    }
    public async Task<bool> IsLoggedAsync(string login, string password)
    {
        return await Users.Where(u => u.Login.Equals(login) && u.Password.Equals(password)).AnyAsync();
    }
    public bool AddUser(User user)
    {
        if (user == null) return false;
        Users.Add(user);
        this.SaveChanges();
        return true;
    }
    public async Task<bool> AddUserAsync(User user)
    {
        if (user == null) return false;
        await Users.AddAsync(user);
        await SaveChangesAsync();
        return true;
    }
    public bool RemoveUser(User user)
    {
        if (user == null) return false;
        var realUser = Users.Where(u => u.Id == user.Id).FirstOrDefault();
        if (realUser == null) return false;
        Users.Remove(realUser);
        this.SaveChanges();
        return true;
    }
    public async Task<bool> RemoveUserAsync(User user)
    {
        if (user == null) return false;
        var realUser = await Users.Where(u => u.Id == user.Id).FirstOrDefaultAsync();
        if (realUser == null) return false;
        Users.Remove(realUser);
        await SaveChangesAsync();
        return true;
    }
    public bool RemoveUser(Guid id)
    {
        var realUser = Users.Where(u => u.Id == id).FirstOrDefault();
        if (realUser == null) return false;
        Users.Remove(realUser);
        this.SaveChanges();
        return true;
    }
    public async Task<bool> RemoveUserAsync(Guid id)
    {
        var realUser = await Users.Where(u => u.Id == id).FirstOrDefaultAsync();
        if (realUser == null) return false;
        Users.Remove(realUser);
        await SaveChangesAsync();
        return true;
    }
    public bool RemoveUserByLogin(string login)
    {
        if (login == null) return false;
        var realUser = Users.Where(u => u.Login == login).FirstOrDefault();
        if (realUser == null) return false;
        Users.Remove(realUser);
        this.SaveChanges();
        return true;
    }
    public async Task<bool> RemoveUserByLoginAsync(string login)
    {
        if (login == null) return false;
        var realUser = await Users.Where(u => u.Login == login).FirstOrDefaultAsync();
        if (realUser == null) return false;
        Users.Remove(realUser);
        await SaveChangesAsync();
        return true;
    }

    public void MakeFriends(Guid firstUserGuid, Guid secondUserGuid)
    {
        var firstUser = Users.Where(u => u.Id == firstUserGuid).FirstOrDefault();
        var SecondUser = Users.Where(u => u.Id == secondUserGuid).FirstOrDefault();

        if (firstUser != null && SecondUser != null)
        {
            firstUser.Friends.Add(secondUserGuid);
            SecondUser.Friends.Add(firstUserGuid);
            SaveChanges();
        }

    }
    public async void MakeFriendsAsync(Guid firstUserGuid, Guid secondUserGuid)
    {
        var firstUser = await Users.Where(u => u.Id == firstUserGuid).FirstOrDefaultAsync();
        var SecondUser = await Users.Where(u => u.Id == secondUserGuid).FirstOrDefaultAsync();

        if (firstUser != null && SecondUser != null)
        {
            firstUser.Friends.Add(secondUserGuid);
            SecondUser.Friends.Add(firstUserGuid);
            await SaveChangesAsync();
        }

    }

}
