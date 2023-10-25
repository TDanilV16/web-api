using System;
using System.Collections.Generic;
using System.Linq;
using AutoMapper;
using Game.Domain;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Newtonsoft.Json;
using WebApi.Models;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : Controller
    {
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        private readonly LinkGenerator linkGenerator;

        // Чтобы ASP.NET положил что-то в userRepository требуется конфигурация
        public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
        {
            this.userRepository = userRepository;
            this.mapper = mapper;
            this.linkGenerator = linkGenerator;
        }


        [HttpGet("{userId}", Name = nameof(GetUserById))]
        [HttpHead("{userId}")]
        [Produces("application/json", "application/xml")]
        public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
        {
            var user = userRepository.FindById(userId);
            if (user is null)
            {
                return NotFound();
            }

            var userDto = mapper.Map<UserDto>(user);

            return Ok(userDto);
        }

        [HttpPost]
        [Produces("application/json", "application/xml")]
        public IActionResult CreateUser([FromBody] PostUserDto postUser)
        {
            if (postUser is null)
                return BadRequest();

            if (postUser.Login is null)
                ModelState.AddModelError("login", "Login must not be null");

            else if (postUser.Login.Any(ch => !char.IsLetterOrDigit(ch)))
                ModelState.AddModelError("login", "Login must contains only letters or digits");

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            var userEntity = mapper.Map<UserEntity>(postUser);
            var createdUserEntity = userRepository.Insert(userEntity);
            return CreatedAtRoute(
                nameof(GetUserById),
                new { userId = createdUserEntity.Id },
                createdUserEntity.Id);
        }

        [HttpPut("{userId}")]
        [Produces("application/json", "application/xml")]
        public IActionResult UpdateUser([FromBody] PutUserDto user, Guid userId)
        {
            if (user is null || userId == Guid.Empty)
                return BadRequest();

            if (!ModelState.IsValid)
                return UnprocessableEntity(ModelState);

            var userEntityWithId = new UserEntity(id: userId);
            var userEntity = mapper.Map(user, userEntityWithId);
            userRepository.UpdateOrInsert(userEntity, isInserted: out var isInserted);

            if (isInserted)
            {
                return CreatedAtRoute(
                    nameof(GetUserById),
                    new { userId = userEntity.Id },
                    userEntity.Id);
            }

            return NoContent();
        }

        [HttpPatch("{userId}")]
        [Produces("application/json", "application/xml")]
        public IActionResult PartiallyUpdateUser([FromBody] JsonPatchDocument<PatchUserDto> patchDoc, Guid userId)
        {
            if (patchDoc is null)
                return BadRequest();

            if (userId == Guid.Empty)
                return NotFound();

            var user = userRepository.FindById(userId);

            if (user is null)
                return NotFound();

            var patchUserDto = new PatchUserDto();
            mapper.Map(user, patchUserDto);
            patchDoc.ApplyTo(patchUserDto, ModelState);

            TryValidateModel(patchUserDto);

            return ModelState.IsValid ? NoContent() : UnprocessableEntity(ModelState);
        }


        [HttpDelete("{userId}")]
        [Produces("application/json", "application/html")]
        public IActionResult DeleteUser(Guid userId)
        {
            if (userId == Guid.Empty)
                return NotFound();

            var user = userRepository.FindById(userId);

            if (user is null)
                return NotFound();

            userRepository.Delete(userId);
            return NoContent();
        }


        [HttpGet(Name = nameof(GetUsers))]
        [Produces("application/json", "application/html")]
        public IActionResult GetUsers([FromQuery] int? pageNumber, [FromQuery] int? pageSize)
        {
            var intPageNumber = pageNumber == null ? 1 : Math.Max(1, pageNumber.Value);
            var intPageSize = pageSize == null ? 10 : Math.Min(20, Math.Max(1, pageSize.Value));

            var pageList = userRepository.GetPage(intPageNumber, intPageSize);

            var paginationHeader = new
            {
                previousPageLink = pageList.HasPrevious
                    ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers),
                        new { pageNuber = pageList.CurrentPage - 1, intPageSize })
                    : null,
                nextPageLink = pageList.HasNext
                    ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers),
                        new { pageNuber = pageList.CurrentPage + 1, intPageSize })
                    : null,
                totalCount = pageList.TotalCount,
                pageSize = pageList.PageSize,
                currentPage = pageList.CurrentPage,
                totalPages = pageList.TotalPages,
            };
            Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));

            var users = mapper.Map<IEnumerable<UserDto>>(pageList);
            return Ok(users);
        }

        [HttpOptions]
        [Produces("application/json", "application/html")]
        public IActionResult UserOptions()
        {
            Response.Headers.Add("Allow", "GET, POST, OPTIONS");
            return Ok();
        }
    }
}