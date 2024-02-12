using ConsoleApp1.Domain.Network;
using ConsoleApp1.Domain.Network.Utils;

internal class Program
{
    private static void Main(string[] args)
    {
        /*using (var db = new ApplicationContext())
        {
            db.Add(new User() { Id = Guid.NewGuid(), Email = "ewfwef", Login = "1" });
            var secGuid = Guid.NewGuid();
            db.Add(new User() { Id = secGuid, Email = "22", Login = "2222" });
            var thirdGuid = Guid.NewGuid();
            db.Add(new User() { Id = thirdGuid, Email = "333", Login = "333" });
            db.SaveChanges();
            var first = db.Users.First();
            first.Friends.Add(db.Users.Where(u => u.Id == secGuid).First().Id);
            first.Friends.Add(db.Users.Where(u => u.Id == thirdGuid).First().Id);
            db.SaveChanges();
            foreach (var item in first.Friends)
            {
                Console.WriteLine(item);
            }
            foreach (var i in db.Users.First().Friends)
            {
                Console.WriteLine(i);
            }
        }*/
        Server sr = new(10000, ServerNetworkInterface.GetServerNetworkInterface(), "VortexServer");
        sr.StartServer();

    }
}