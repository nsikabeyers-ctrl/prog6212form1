# RaceDay API Endpoint Plan

Roles: **None** = public, **Any** = any logged-in user, **Organiser**, **Participant**.
Not logged in always returns 401. Logged in with the wrong role returns 403.

## Changes made while building Part 2

The Part 1 plan was followed. These are the differences, and why.

| Change | Reason |
|---|---|
| `POST /api/auth/register` also takes `organiserInviteCode` (Organisers only) and returns 403 if it is wrong | Without it anyone could register as an Organiser and gain organiser powers. |
| Added `GET /api/events/mine` | The Part 3 Organiser dashboard needs only the events that Organiser created, past and upcoming. |
| Added `PUT /api/enrolments/{id}/status` | Enrolments start as Pending. The Organiser needs a way to confirm or cancel them. |
| `DELETE /api/events/{id}` and `DELETE /api/categories/{id}` return 409 when enrolments exist | The database does not allow deleting rows that other rows depend on. |
| `GET /api/events` hides past events unless `includePast=true` | The plan said "upcoming events". |
| `POST /api/profile/picture` and `POST /api/events/{id}/banner` are not built in Part 2 | These need Azure Blob Storage, which the brief puts in Part 3. |

## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Registers a new user as an Organiser or a Participant. Organisers must supply the invite code. | None | { fullName, email, password, role, dateOfBirth?, phoneNumber?, organiserInviteCode? } | 201 Created – new user id and role · 400 Bad Request – validation failed or unknown role · 403 Forbidden – missing or wrong organiser invite code · 409 Conflict – email already registered |
| POST | /api/auth/login | Checks the credentials and starts a session holding the user id and role. | None | { email, password } | 200 OK – user id and role, session cookie set · 400 Bad Request · 401 Unauthorized – invalid credentials |
| POST | /api/auth/logout | Ends the current session. | Any | None | 200 OK – session cleared · 401 Unauthorized |

## User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/profile | Returns the logged-in user's own profile. | Any | None | 200 OK – profile details · 401 Unauthorized |
| PUT | /api/profile | Updates the logged-in user's own profile. Organisers can change their phone number, Participants their date of birth. | Any | { fullName, phoneNumber?, dateOfBirth? } | 200 OK – updated profile · 400 Bad Request · 401 Unauthorized |
| POST | /api/profile/picture | Uploads a profile picture. **Built in Part 3 with Blob Storage.** | Participant | multipart file | 200 OK – picture URL |

## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events | Lists upcoming events. Optional query: type, search, from, to, includePast. | Any | None | 200 OK – array of events with categories · 401 Unauthorized |
| GET | /api/events/mine | Lists every event created by the logged-in Organiser. | Organiser | None | 200 OK – array of events · 401 Unauthorized · 403 Forbidden |
| GET | /api/events/{id} | Returns one event with its categories. | Any | None | 200 OK – event details · 401 Unauthorized · 404 Not Found |
| POST | /api/events | Creates an event owned by the logged-in Organiser. | Organiser | { name, description, eventDate, location, distanceKm, eventType } | 201 Created – the new event · 400 Bad Request – invalid data or date in the past · 401 Unauthorized · 403 Forbidden |
| PUT | /api/events/{id} | Updates an event the Organiser owns. | Organiser | { name, description, eventDate, location, distanceKm, eventType } | 200 OK – updated event · 400 Bad Request · 401 Unauthorized · 403 Forbidden – not the owner · 404 Not Found |
| DELETE | /api/events/{id} | Deletes an event the Organiser owns, along with its categories. | Organiser | None | 204 No Content · 401 Unauthorized · 403 Forbidden · 404 Not Found · 409 Conflict – event has enrolments |
| POST | /api/events/{id}/banner | Uploads a banner image. **Built in Part 3 with Blob Storage.** | Organiser | multipart file | 200 OK – banner URL |

`eventType` is one of `Run`, `Walk`, `Cycle`.

## Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/categories | Lists the categories for an event. | Any | None | 200 OK – array of categories · 401 Unauthorized · 404 Not Found |
| POST | /api/events/{eventId}/categories | Adds an age or distance category to an event the Organiser owns. | Organiser | { name, minAge?, maxAge?, distanceKm? } | 201 Created – the new category · 400 Bad Request – for example minAge above maxAge · 401 Unauthorized · 403 Forbidden · 404 Not Found |
| PUT | /api/categories/{id} | Updates a category. | Organiser | { name, minAge?, maxAge?, distanceKm? } | 200 OK – updated category · 400 Bad Request · 401 Unauthorized · 403 Forbidden · 404 Not Found |
| DELETE | /api/categories/{id} | Removes a category. | Organiser | None | 204 No Content · 401 Unauthorized · 403 Forbidden · 404 Not Found · 409 Conflict – participants are enrolled in it |

## Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/events/{eventId}/enrolments | Enters the logged-in Participant into the event under a chosen category. Starts as Pending. | Participant | { categoryId } | 201 Created – the enrolment · 400 Bad Request – category belongs to another event, age outside the category range, or the event has passed · 401 Unauthorized · 403 Forbidden · 404 Not Found · 409 Conflict – already enrolled |
| GET | /api/enrolments/mine | Lists the logged-in Participant's own enrolments. | Participant | None | 200 OK – array of enrolments · 401 Unauthorized · 403 Forbidden |
| GET | /api/events/{eventId}/enrolments | Lists all enrolments for an event the Organiser owns. | Organiser | None | 200 OK – array of enrolments · 401 Unauthorized · 403 Forbidden – not the owner · 404 Not Found |
| PUT | /api/enrolments/{id}/status | Sets an enrolment to Pending, Confirmed or Cancelled. | Organiser | { status } | 200 OK – updated enrolment · 400 Bad Request · 401 Unauthorized · 403 Forbidden · 404 Not Found |
| DELETE | /api/enrolments/{id} | Withdraws the logged-in Participant from an event. | Participant | None | 204 No Content · 401 Unauthorized · 403 Forbidden – not their enrolment · 404 Not Found · 409 Conflict – a result already exists |

## Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{enrolmentId}/result | Captures a finish time and position for an enrolment. | Organiser | { finishTime, finishPosition } | 201 Created – the result · 400 Bad Request · 401 Unauthorized · 403 Forbidden – not the owner · 404 Not Found · 409 Conflict – result already captured |
| PUT | /api/results/{id} | Corrects a result that was captured earlier. | Organiser | { finishTime, finishPosition } | 200 OK – updated result · 400 Bad Request · 401 Unauthorized · 403 Forbidden · 404 Not Found |
| GET | /api/results/mine | Returns the logged-in Participant's race history, including total finishers per event. | Participant | None | 200 OK – array of results · 401 Unauthorized · 403 Forbidden |
| GET | /api/events/{eventId}/results | Lists all results for an event in finishing order. | Any | None | 200 OK – array of results · 401 Unauthorized · 404 Not Found |

`finishTime` is written as `hh:mm:ss`, for example `00:48:32`.
