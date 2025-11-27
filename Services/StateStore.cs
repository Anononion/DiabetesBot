public static class StateStore
{
    private static readonly Dictionary<long, UserData> _users = new();

    public static UserData Get(long id)
    {
        if (!_users.TryGetValue(id, out var user))
        {
            user = new UserData
            {
                UserId = id,
                Language = "ru",
                Phase = BotPhase.MainMenu
            };
            _users[id] = user;
        }

        return user;
    }
}
