﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SFML.Graphics;
using SFML.Window;

namespace Californium
{
    public static class Game
    {
        public static RenderWindow Window;
        public static View DefaultView
        {
            get
            {
                var view = Window.DefaultView;
                view.Size = size;
                return view;
            }
        }

        private static List<State> states;
        private static bool[] keyStates;
        private static Vector2f size;

        static Game()
        {
            states = new List<State>();
            keyStates = new bool[(int)Keyboard.Key.KeyCount];
        }

        public static void Initialize()
        {
            var style = Styles.Titlebar | Styles.Close;
            if (GameOptions.Resizable)
                style |= Styles.Resize;

            size = new Vector2f(GameOptions.Width, GameOptions.Height);

            Window = new RenderWindow(new VideoMode(GameOptions.Width, GameOptions.Height), GameOptions.Caption, style);
            Window.SetFramerateLimit(GameOptions.Framerate);
            Window.SetVerticalSyncEnabled(GameOptions.Vsync);

			if (GameOptions.Icon != "")
			{
				Texture texture = Assets.LoadTexture(GameOptions.Icon);
				Window.SetIcon(texture.Size.X, texture.Size.Y, texture.CopyToImage().Pixels);
			}
            
            Window.Closed += (sender, args) => Window.Close();
            Window.Resized += (sender, args) => Resize(new Vector2f(args.Width, args.Height));
            Window.MouseButtonPressed += (sender, args) => DispatchEvent(new MouseButtonInputArgs(args.Button, true, args.X, args.Y));
            Window.MouseButtonReleased += (sender, args) => DispatchEvent(new MouseButtonInputArgs(args.Button, false, args.X, args.Y));
            Window.MouseWheelMoved += (sender, args) => DispatchEvent(new MouseWheelInputArgs(args.Delta, args.X, args.Y));
            Window.MouseMoved += (sender, args) => DispatchEvent(new MouseMoveInputArgs(args.X, args.Y));
            Window.TextEntered += (sender, args) => DispatchEvent(new TextInputArgs(args.Unicode));

            Window.KeyPressed += (sender, args) =>
            {
                if (args.Code == Keyboard.Key.Unknown || keyStates[(int)args.Code]) // repeated key press
                    return; 
                keyStates[(int)args.Code] = true;
                DispatchEvent(new KeyInputArgs(args.Code, true, args.Control, args.Shift));
            };

            Window.KeyReleased += (sender, args) =>
            {
                if (args.Code != Keyboard.Key.Unknown)
                    keyStates[(int)args.Code] = false;
                DispatchEvent(new KeyInputArgs(args.Code, false, args.Control, args.Shift));
            };
        }

        public static void Run()
        {
            var timer = new Stopwatch();
            double accumulator = 0;

            while (Window.IsOpen())
            {
                var time = timer.Elapsed.TotalSeconds;
                timer.Restart();

                accumulator += time;

                // Update
                while (accumulator >= GameOptions.Timestep)
                {
                    Window.DispatchEvents();
                    Timer.Update();

                    for (var i = 0; i < states.Count; i++)
                    {
                        var state = states[i];

                        if (i == states.Count - 1 || state.InactiveMode.HasFlag(State.UpdateMode.Update))
                            state.UpdateInternal();
                    }

                    accumulator -= GameOptions.Timestep;
                }


                // Draw
                var clearState = states.FindIndex(s => s.InactiveMode.HasFlag(State.UpdateMode.Draw)); 
                for (var i = 0; i < states.Count; i++)
                {
                    var state = states[i];

                    if (i == clearState)
                        Window.Clear(state.ClearColor);

                    if (i != states.Count - 1 && !state.InactiveMode.HasFlag(State.UpdateMode.Draw))
                        continue;

                    state.Draw(Window);
                }

                Window.Display();
            }
        }

        public static bool Exit()
        {
            Window.Close();

	        return true;
        }

        public static void SetState(State state)
        {
            foreach (var s in states)
            {
                s.Leave();
            }

            states.Clear();
            PushState(state);
        }

        public static void PushState(State state)
        {
			states.Add(state);
            state.Enter();
        }

        public static void PopState()
        {
			if (states.Count == 1)
				return;

            var last = states.Count - 1;
            states[last].Leave();
            states.RemoveAt(last);
        }

		public static State PeekState()
		{
			return states.Last();
		}

		public static State PeekFirstState()
		{
			return states.First();
		}

        private static void DispatchEvent(InputArgs args)
        {
            for (var i = states.Count - 1; i >= 0; i--)
            {
                var state = states[i];

                args.View = state.Camera.View;
                if (states[i].ProcessEvent(args))
                    return;
            }
        }

        private static void Resize(Vector2f newSize)
        {
            size = newSize;

            foreach (var state in states)
            {
                state.InitializeCamera();
            }
        }

        internal static bool IsActive(State state)
        {
            return states.IndexOf(state) == (states.Count - 1);
        }
    }
}
