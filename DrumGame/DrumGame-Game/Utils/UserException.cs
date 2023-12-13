using System;

namespace DrumGame.Game.Utils;

// exception when the user messes up. Message should be user friendly
public class UserException : Exception
{
    public UserException(string message) : base(message)
    {
    }
}