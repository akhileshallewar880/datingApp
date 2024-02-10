﻿using System.Security.Claims;
using API.DTO;
using API.Entity;
using API.Interfaces;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

[Authorize]
public class  UsersController : BaseApiController
{
    private readonly IUserRepository userRepository1;

    private readonly IPhotoService photoService1;

    private readonly IMapper mapper1;
    public UsersController(IUserRepository  userRepository, IMapper mapper, IPhotoService photoService)
    {
        userRepository1 = userRepository;

        mapper1 = mapper;

        photoService1 = photoService;
    }

    [AllowAnonymous]
    [HttpGet]  // api/users
    public async Task<ActionResult<IEnumerable<MemberDTO>>> GetUsers()
    {
        var users = await userRepository1.GetMembersAsync();

        return Ok(users);
    }

    [HttpGet("{username}")] //api/users/1
    public async Task<ActionResult<MemberDTO>> GetUser(string username)
    {
        return await userRepository1.GetMemberAsync(username);

    }

    [HttpPut]
    public async Task<ActionResult> UpdateUser(MemberUpdateDto memberUpdateDto)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var user = await userRepository1.GetUserByUsernameAsync(username);

        if(user == null) return NotFound();

        mapper1.Map(memberUpdateDto, user);

        if(await userRepository1.SaveAllAsync()) return NoContent();

        return BadRequest("Failed to update user");

    }

    [HttpPost("add-photo")]
    public async Task<ActionResult<PhotoDTO>> AddPhoto(IFormFile file)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var user = await userRepository1.GetUserByUsernameAsync(username);

        if(user == null) return NotFound();

        var result = await photoService1.AddPhotoAsync(file);

        if(result.Error != null) return BadRequest(result.Error.Message);

        var photo = new Photo
        {
            Url = result.SecureUrl.AbsoluteUri,
            PublicId = result.PublicId
        };

        if(user.Photos.Count == 0) photo.IsMain = true;

        user.Photos.Add(photo);

        if(await userRepository1.SaveAllAsync())
        {
            return CreatedAtAction(nameof(GetUser),
            new {username = user.UserName}, mapper1.Map<PhotoDTO>(photo));
        }

        return BadRequest("Problem adding photo");
    }

    [HttpPut("set-main-photo/{photoId}")]
    public async Task<ActionResult> SetMainPhoto(int photoId)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var user = await userRepository1.GetUserByUsernameAsync(username);

        if(user == null) return NotFound();

        var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

        if(photo == null) return BadRequest("this is already your main photo");

        var currentMain = user.Photos.FirstOrDefault(x => x.IsMain);
        if(currentMain != null) currentMain.IsMain = false;
        photo.IsMain = true;

        if(await userRepository1.SaveAllAsync()) return NoContent();

        return BadRequest("Problem setting the main photo");
        
    }

    [HttpDelete("delete-photo/{photoId}")]
    public async Task<ActionResult> DeletePhoto(int photoId)
    {
        var username = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var user = await userRepository1.GetUserByUsernameAsync(username);

        var photo = user.Photos.FirstOrDefault(x => x.Id == photoId);

        if(photo == null) return NotFound();

        if(photo.IsMain) return BadRequest("You cannot delete you main photo");

        if(photo.PublicId != null)
        {
            var result = await photoService1.DeletePhotoAsync(photo.PublicId);
            if(result.Error != null) return BadRequest(result.Error.Message);
        }

        user.Photos.Remove(photo);

        if(await userRepository1.SaveAllAsync()) return Ok();

        return BadRequest("Unable to delete photo");
    }
}
