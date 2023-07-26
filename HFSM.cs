#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox;

public interface IState {}
public class Transition<TEvent> where TEvent: struct, Enum
{
    private IState m_to;
    private Func<bool> m_guard = () => true;
    private TEvent? m_event = null;
	
    public Transition(IState to)
    {
        m_to = to;
    }
    public Transition(IState to, TEvent @event) : this(to)
    {
        m_event = @event;
    }
    public Transition(IState to, Func<bool> guard) : this(to)
    {
        m_guard = guard;
    }
    public Transition(IState to, TEvent? @event, Func<bool> guard) : this(to)
    {
        m_guard = guard;
        m_event = @event;
    }

    public bool MatchConditions(TEvent? fsmEvent)
    {
        return m_guard() && m_event.Equals(fsmEvent);
    }
    public IState Destination()
    {
        return m_to;
    }
}
public class State<TName, TEvent> : IState where TEvent: struct, Enum where TName: Enum
{
    private static readonly Action? NoActivity = () => { };
    public State(TName name)
    {
        m_name = name;
    }
    
    public State(TName name, State<TName, TEvent>? root = null, Action? onEnterAction = null, Action? onUpdateAction = null,
        Action? onExitAction = null)
        : this(name)
    {
        m_parentState = root;
        root?.AddChild(this);
        
        if (onEnterAction != null) m_onEnterAction = onEnterAction;
        if (onUpdateAction != null) m_onUpdateAction = onUpdateAction;
        if (onExitAction != null) m_onExitAction = onExitAction;
    }
    
    public State(TName name, Action? onEnterAction = null, Action? onUpdateAction = null,
        Action? onExitAction = null)
        : this(name)
    {
        if (onEnterAction != null) m_onEnterAction = onEnterAction;
        if (onUpdateAction != null) m_onUpdateAction = onUpdateAction;
        if (onExitAction != null) m_onExitAction = onExitAction;
    }

    private TName m_name;

    private Action? m_onEnterAction = NoActivity;
    private Action? m_onUpdateAction = NoActivity;
    private Action? m_onExitAction = NoActivity;

    private State<TName, TEvent>? m_parentState = null;
    private List<State<TName, TEvent>> m_children = new();
    private List<Transition<TEvent>> m_transitions = new();

    public void AddTransition(Transition<TEvent> transition)
    {
        m_transitions.Add(transition);
    }
    public State<TName, TEvent>? CheckTransitions(TEvent? fsmEvent)
    {
        foreach (Transition<TEvent> transition in m_transitions)
        {
	        if ( transition.MatchConditions( fsmEvent ) )
		        return transition.Destination() as State<TName, TEvent>;
        }
        return null;
    }
    
    public List<State<TName, TEvent>> GetChildren()
    {
        return m_children;
    }
    private void AddChild(State<TName, TEvent> state) { m_children.Add(state); }
    
    public void SetParent(State<TName,TEvent> state)
    {
        m_parentState = state;
        m_parentState.AddChild(this);
    }
    
    public State<TName, TEvent>? GetParent()
    {
        return m_parentState;
    }

    public TName Name { get => m_name; set => m_name = value; }
    public void OnEnter() { m_onEnterAction?.Invoke(); }
    public void OnUpdate() { m_onUpdateAction?.Invoke(); }
    public void OnExit() { m_onExitAction?.Invoke(); }

    public override string ToString()
    {
	    return Name.ToString();
    }
}

public class HFSMBuilder<TName, TEvent> where TEvent : struct, Enum where TName : Enum
{
    private readonly Dictionary<TName, State<TName, TEvent>> m_states = new();
    private readonly List<TransitionInfo> m_transitionInfos = new();
    private readonly Dictionary<TName, TName> m_stateParents = new();

    private class TransitionInfo
    {
        private TEvent? m_trigger = null;

        public TName From { get; set; }

        public TName To { get; set; }

        public Func<bool>? Guard { get; set; }

        public TEvent? Trigger
        {
            get => m_trigger;
            set => m_trigger = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        public TransitionInfo(TName from, TName to)
        {
            From = from;
            To = to;
        }

        public TransitionInfo(TName from, TName to, Func<bool>? guard)
        {
            From = from;
            To = to;
            Guard = guard;
        }
        
        public TransitionInfo(TName from, TName to, Func<bool>? guard, TEvent @event)
        {
            From = from;
            To = to;
            Guard = guard;
            m_trigger = @event;
        }
        
        public TransitionInfo(TName from, TName to, TEvent @event)
        {
            From = from;
            To = to;
            m_trigger = @event;
        }
    }
    
    public void AddState(TName name, Action? onEnterAction = null, Action? onUpdateAction = null, Action? onExitAction = null)
    {
        m_stateParents.Add(name, name);
        m_states.Add(name, new State<TName, TEvent>(name, onEnterAction, onUpdateAction, onExitAction));
    }
    
    public void AddState(TName name, TName parentName, Action? onEnterAction = null, Action? onUpdateAction = null, Action? onExitAction = null)
    {
        if ( m_states.ContainsKey( name ) )
        {
	        throw new ArgumentException( $"State already defined {name}" );
        }
        
        m_stateParents.Add(name, parentName);
        m_states.Add(name, new State<TName, TEvent>(name, onEnterAction, onUpdateAction, onExitAction));
    }
    
    public void AddTransition(TName from, TName to)
    {
        m_transitionInfos.Add(new TransitionInfo(from, to));
    }
    public void AddTransition(TName from, TName to, TEvent @event)
    {
        m_transitionInfos.Add(new TransitionInfo(from, to, @event));
    }
    
    public void AddTransition(TName from, TName to, Func<bool> guard)
    {
        m_transitionInfos.Add(new TransitionInfo(from, to, guard));
    }
    
    public void AddTransition(TName from, TName to, Func<bool> guard, TEvent @event)
    {
        m_transitionInfos.Add(new TransitionInfo(from, to, guard, @event));

    }

    public HFSM<TName, TEvent> Build()
    {
        State<TName, TEvent>? initial = null;
        
        foreach (var (name,state) in m_states)
        {
            if (!Equals(m_stateParents[name], name))
            {
                state.SetParent(m_states[m_stateParents[name]]);
            }
            else if (initial == null)
            {
                initial = state;
            }
        }
        
        foreach (var transitionInfo in m_transitionInfos)
        {
            State<TName, TEvent> from = m_states[transitionInfo.From];
            State<TName, TEvent> to = m_states[transitionInfo.To];
            
            if (transitionInfo is { Trigger: not null, Guard: not null })
            {
                from.AddTransition(new Transition<TEvent>(to, transitionInfo.Trigger.Value, transitionInfo.Guard));
            }
            else if(transitionInfo.Trigger.HasValue)
            {
                from.AddTransition(new Transition<TEvent>(to, transitionInfo.Trigger.Value));
            }
            else if(transitionInfo.Guard != null)
            {
                from.AddTransition(new Transition<TEvent>(to, transitionInfo.Guard));
            }
            else
            {
                from.AddTransition(new Transition<TEvent>(to));
            }
        }
		Log.Info( $"[HFSM] HFSM Built with initial state: {initial}" );
        return new HFSM<TName, TEvent>(initial ?? throw new InvalidOperationException("No root state detected !"));
    }
}
public class HFSM<TName, TEvent> where TEvent: struct, Enum where TName: Enum 
{
    private State<TName, TEvent> m_initialState;
    private State<TName, TEvent>? m_currentState;

    private Queue<TEvent?> m_eventToTreat = new();
    
    public string GetDebugCurrentStateName() => m_currentState?.Name.ToString() ?? "No active current state";
    
    public HFSM(State<TName, TEvent> initial)
    {
        m_initialState = initial;
    }
    
    public List<State<TName, TEvent>> GetActiveStatesHierarchy()
    {
        return m_currentState == null ? new List<State<TName, TEvent>>() : GetStatesHierarchy(m_currentState);
    }

    private List<State<TName, TEvent>> GetStatesHierarchy(State<TName, TEvent> root)
    {
        List<State<TName, TEvent>> activeHierarchy = new List<State<TName, TEvent>>();
        
        for (State<TName, TEvent>? parent = root; parent != null; parent = parent.GetParent())
        {
            activeHierarchy.Insert(0, parent);
        }
        
        return activeHierarchy;
    }

    private static State<TName, TEvent> GetFirstLeaf(State<TName, TEvent> state)
    {
        State<TName, TEvent> returnValue = state;
        while (returnValue.GetChildren().Count > 0)
        {
            returnValue = returnValue.GetChildren().First();
        }
        return returnValue;
    }
   
    public void Start()
    {
        ChangeActiveState(m_initialState);
    }
    
    public void Update()
    {
        if (m_currentState == null)
        {
            Start();
            return;
        }

        m_eventToTreat.TryDequeue(out var receivedEvent);
        
        foreach (State<TName, TEvent> state in GetActiveStatesHierarchy())
        {
            State<TName, TEvent>? destinationState = state.CheckTransitions(receivedEvent);
            if (destinationState != null)
            {
                ChangeActiveState(destinationState);
                break;
            }
            state.OnUpdate();
        }
    }
    
    public void SendEvent(TEvent trigger)
    {
        m_eventToTreat.Enqueue(trigger);
    }

    private void ChangeActiveState(State<TName, TEvent>? newState)
    {
        State<TName, TEvent>? nextState = newState != null ? GetFirstLeaf(newState) : null;
        
        HashSet<State<TName, TEvent>> activeStatesHierarchy = new HashSet<State<TName, TEvent>>(GetActiveStatesHierarchy());
        HashSet<State<TName, TEvent>> futureStatesHierarchy = nextState != null ? new HashSet<State<TName, TEvent>>(GetStatesHierarchy(nextState)) : new ();
        
        List<State<TName, TEvent>> exitingStates = activeStatesHierarchy.Except(futureStatesHierarchy).ToList();
        for (int index = exitingStates.Count - 1; index >= 0; --index)
        {
            exitingStates[index].OnExit();
        }
        
        m_currentState = nextState;
        
        List<State<TName, TEvent>> enteringStates = futureStatesHierarchy.Except(activeStatesHierarchy).ToList();
        foreach (State<TName, TEvent> state in enteringStates)
        {
            state.OnEnter();
        }
    }
    public void Stop()
    {
        ChangeActiveState(null);
    }
    
    ~HFSM()
    {
        Stop();
    }
}

