using Gamefilled.Application.Social;
using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.users;

public sealed class FollowingModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly SocialListService _socialLists;

    public FollowingModel(AppDbContext db, SocialListService socialLists)
    {
        _db = db;
        _socialLists = socialLists;
    }

    public User ProfileUser { get; private set; } = default!;
    public IReadOnlyList<SocialListUserCard> Users { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
            return NotFound();

        var profileUser = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Username == username, cancellationToken);

        if (profileUser is null)
            return NotFound();

        ProfileUser = profileUser;
        Users = await _socialLists.GetFollowingAsync(profileUser.Id, cancellationToken);
        return Page();
    }
}
