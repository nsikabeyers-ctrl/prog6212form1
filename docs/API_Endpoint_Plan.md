# RaceDay API Endpoint Plan — Part 1

Roles referenced below: **None** = public, **Any** = any authenticated user, **Organiser**, **Participant**.

## Authentication

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/auth/register | Registers a new user as either an Organiser or a Participant. | None | { fullName, email, password, role, dateOfBirth? } | 201 Created – new user id and role · 400 Bad Request – validation failed · 409 Conflict – email already registered |
| POST | /api/auth/login | Authenticates a user and starts a session storing their id and role. | None | { email, password } | 200 OK – session started, user id and role returned · 401 Unauthorized – invalid credentials |
| POST | /api/auth/logout | Ends the current session. | Any | None | 200 OK – session cleared |

## User Profile

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/profile | Returns the logged-in user's own profile. | Any | None | 200 OK – profile details · 401 Unauthorized |
| PUT | /api/profile | Updates the logged-in user's own profile details. | Any | { fullName, phoneNumber?, dateOfBirth? } | 200 OK – updated profile · 400 Bad Request |
| POST | /api/profile/picture | Uploads/replaces the Participant's profile picture. | Participant | multipart file | 200 OK – picture URL · 400 Bad Request |

## Events

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events | Lists all upcoming events, with optional filters. | Any | None | 200 OK – array of events |
| GET | /api/events/{id} | Returns full details for one event, including its categories. | Any | None | 200 OK – event details · 404 Not Found |
| POST | /api/events | Creates a new event. | Organiser | { name, description, eventDate, location, distanceKm, eventType } | 201 Created – new event id · 400 Bad Request |
| PUT | /api/events/{id} | Updates an event the Organiser owns. | Organiser | { name, description, eventDate, location, distanceKm, eventType } | 200 OK – updated event · 403 Forbidden – not the owner · 404 Not Found |
| DELETE | /api/events/{id} | Deletes an event the Organiser owns. | Organiser | None | 204 No Content · 403 Forbidden · 404 Not Found |
| POST | /api/events/{id}/banner | Uploads a banner image for the event. | Organiser | multipart file | 200 OK – banner URL · 403 Forbidden · 404 Not Found |

## Categories

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| GET | /api/events/{eventId}/categories | Lists all categories for an event. | Any | None | 200 OK – array of categories · 404 Not Found |
| POST | /api/events/{eventId}/categories | Adds a category to an event. | Organiser | { name, minAge?, maxAge?, distanceKm? } | 201 Created – new category id · 403 Forbidden · 404 Not Found |
| PUT | /api/categories/{id} | Updates a category. | Organiser | { name, minAge?, maxAge?, distanceKm? } | 200 OK – updated category · 403 Forbidden · 404 Not Found |
| DELETE | /api/categories/{id} | Removes a category. | Organiser | None | 204 No Content · 403 Forbidden · 404 Not Found |

## Event Enrolments

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/events/{eventId}/enrolments | Enrols the logged-in Participant into the event under a chosen category. | Participant | { categoryId } | 201 Created – enrolment record · 400 Bad Request · 409 Conflict – already enrolled |
| GET | /api/enrolments/mine | Lists the logged-in Participant's own enrolments. | Participant | None | 200 OK – array of enrolments |
| GET | /api/events/{eventId}/enrolments | Lists all enrolments for an event the Organiser owns. | Organiser | None | 200 OK – array of enrolments · 403 Forbidden · 404 Not Found |
| DELETE | /api/enrolments/{id} | Cancels the logged-in Participant's own enrolment. | Participant | None | 204 No Content · 403 Forbidden · 404 Not Found |

## Results

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| POST | /api/enrolments/{enrolmentId}/result | Captures a finish time and position for a Participant's enrolment. | Organiser | { finishTime, finishPosition } | 201 Created – result record · 403 Forbidden · 404 Not Found · 409 Conflict – result already captured |
| PUT | /api/results/{id} | Corrects a previously captured result. | Organiser | { finishTime, finishPosition } | 200 OK – updated result · 403 Forbidden · 404 Not Found |
| GET | /api/results/mine | Lists the logged-in Participant's full personal race history. | Participant | None | 200 OK – array of results |
| GET | /api/events/{eventId}/results | Lists all published results for an event. | Any | None | 200 OK – array of results · 404 Not Found |
