﻿using AutoMapper;
using Events.Business.Interfaces;
using Events.Data.DTOs;
using Events.Data.DTOs.Request;
using Events.Data.Interfaces;
using Events.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Events.Data.Enums;

namespace Events.Business.Services
{
    public class EventService : IEventService
    {
        private readonly IEventRepository _eventRepository;
        private readonly IMapper _mapper;

        public EventService(IEventRepository eventRepository, IMapper mapper)
        {
            _eventRepository = eventRepository;
            _mapper = mapper;
        }

        public async Task<List<EventDTO>> GetAllEvents()
        {
            var events = await _eventRepository.GetAllEvents();
            return _mapper.Map<List<EventDTO>>(events);
        }

        public async Task<EventDTO> CreateEvent(CreateEventDTO createEventDTO)
        {
            var newEvent = _mapper.Map<Event>(createEventDTO);
            await _eventRepository.Add(newEvent);
            await _eventRepository.SaveChangesAsync();
            return _mapper.Map<EventDTO>(newEvent);
        }

        public async Task<EventDTO> GetEventById(int id)
        {
            var eventEntity = await _eventRepository.GetEventById(id);
            return _mapper.Map<EventDTO>(eventEntity);
        }
        public async Task UpdateStatus(int id, EventStatus newStatus)
        {
            var eventEntity = await _eventRepository.GetEventById(id);
            if (eventEntity != null)
            {
                eventEntity.EventStatus = (int)newStatus;
                await _eventRepository.UpdateStatus(eventEntity);
            }
        }

        public async Task UpdateEventDetails(int id, CreateEventDTO updateEventDTO)
        {
            var eventEntity = await _eventRepository.GetEventById(id);
            if (eventEntity == null)
            {
                throw new KeyNotFoundException("Event not found");
            }

            // Use AutoMapper or similar tool to map properties from DTO to entity
            _mapper.Map(updateEventDTO, eventEntity);
            eventEntity.Id = id; // Ensure the ID is set correctly

            await _eventRepository.UpdateEvent(eventEntity);
        }

        public async Task<List<EventDTO>> GetEventsByStatus(EventStatus status)
        {
            var events = await _eventRepository.GetAllEvents();
            if (events == null)
            {
                return new List<EventDTO>();
            }

            var filteredEvents = events.Where(e => e.EventStatus == (int)status).ToList();
            return _mapper.Map<List<EventDTO>>(filteredEvents);
        }

        public async Task DeleteEvent(int id)
        {
            var eventToDelete = await _eventRepository.GetEventById(id);
            if (eventToDelete != null)
            {
                await _eventRepository.DeleteEvent(id);
            }
        }
        public async Task<IEnumerable<EventDTO>> SearchEventsByNameAsync(string eventName)
        {
            var events = await _eventRepository.SearchEventsByNameAsync(eventName);
            return _mapper.Map<IEnumerable<EventDTO>>(events);
        }
        public async Task<string> GetEventNameByIdAsync(int eventId)
        {
            var eventEntity = await _eventRepository.GetEventByIdAsync(eventId);
            if (eventEntity == null)
            {
                throw new KeyNotFoundException("Event not found");
            }
            return eventEntity.Name;
        }
    }
}
