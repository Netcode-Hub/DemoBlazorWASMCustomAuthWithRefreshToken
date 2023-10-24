using DemoBlazorWASMCustomAuthWithRefreshToken.Shared.Models;

namespace DemoBlazorWASMCustomAuthWithRefreshToken.Client.ClientServices
{
    public interface IClientService
    {
        // Public Interfaces
        Task<object> RegisterAccountAsync(RegisterModel model);
        Task<UserSession> LoginAsync(Login model);


        //Protected Interfaces
        Task<int> GetUserCount();
        Task<string> GetMyInfo(string email);
    }
}
